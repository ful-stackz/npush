module Npush.Utilities

open Npush.Types
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open Spectre.Console
open System

/// Get the full path by the name of a file in the current working directory.
let getFilePath name =
    Path.Join [| Directory.GetCurrentDirectory(); name |]

/// Get the contents of a file by providing it's path.
/// If the file does not exist returns None.
let getFileContents path =
  if File.Exists path
  then Some (File.ReadAllText path)
  else None

/// Parses the provided text into a JSON object.
/// If the parsing fails returns None.
let parseJson text =
  try
    Some (JObject.Parse text)
  with
    | _ -> None

/// Creates a new PackageJson from the provided json.
let asPackageJson (json: JObject) : Result<PackageJson, string> =
  let nameField =
    if json.ContainsKey "name"
    then Some (json.Value<string> "name")
    else None

  let versionField =
    if json.ContainsKey "version"
    then Some (json.Value<string> "version")
    else None

  match (nameField, versionField) with
  | (None, _) -> Error "Required field 'name' is missing from package.json"
  | (_, None) -> Error "Required field 'version' is missing from package.json"
  | (Some name, Some versionRaw) ->
    let m = Regex.Match (versionRaw, "(\d+)\.(\d+)\.(\d+)-?(alpha|beta|preview|rc)?")
    if not m.Success
    then Error "Field 'version' has an invalid format"
    else Ok {
      name = name
      version = {
        semver = {
          major = int m.Groups.[1].Value
          minor = int m.Groups.[2].Value
          patch = int m.Groups.[3].Value
        }
        stage =
          match m.Groups.[4].Value with
          | "rc" | "preview" -> Stage.Preview
          | "beta" -> Stage.Beta
          | "alpha" -> Stage.Alpha
          | _ -> Stage.Release
      }
    }

/// Prints the provided reason and terminates the program with the provided code.
let terminate (reason: string) (code: int) =
  AnsiConsole.MarkupLine $"❌ [red]{reason}[/]"
  exit code

/// Formats a SemVer as a string.
let fSemVer (version: SemVer) =
  $"{version.major}.{version.minor}.{version.patch}"

/// Creates a readable version string from a SemVer version and a Stage.
let fVer (version: SemVer) (stage: Stage) =
  let semVer = fSemVer version
  match stage with
  | Stage.Release -> $"{semVer}"
  | Stage.Preview -> $"{semVer}-preview"
  | Stage.Beta -> $"{semVer}-beta"
  | Stage.Alpha -> $"{semVer}-alpha"

/// Creates a standalone readable Stage string.
let fStage (stage: Stage) =
  match stage with
  | Stage.Release -> ""
  | Stage.Preview -> "Preview"
  | Stage.Beta -> "Beta"
  | Stage.Alpha -> "Alpha"

/// Tries to find the value of an argument with the provided `name` in `argv`.
/// Returns `None` if the argument is a flag, eg. given args `--flag --another-arg value`,
/// `findArg` will return `None` for `--flag`. For flags use `findFlag`.
let findArg (name: string) (argv: string array) =
  let names = name.Split "|"
  let matchName n =
    if n = $"--{names.[0]}" then true
    elif names.Length = 2 then n = $"-{names.[1]}"
    else false
  let matchValue (value: string option) =
    match value with
    | Some v -> if v.StartsWith "-" then None else Some v
    | None -> None

  match (Array.tryFindIndex matchName argv) with
  | Some i -> Array.tryItem (i + 1) argv |> matchValue
  | None -> None

/// Tries to find a flag arg in the provided `argv` array.
/// If the arg name is found checks maps the value (if any) to a boolean:
/// 1. no value -> true `--flag`
/// 2. true/t -> true `--flag true` or `--flag t`
/// 3. everything else -> false `--flag false`
let findFlag (name: string) (argv: string array) =
  let names = name.Split "|"
  let matchName n =
    if n = $"--{names.[0]}" then true
    elif names.Length = 2 then n = $"-{names.[1]}"
    else false
  let parseValue (value: string option) =
    match value with
    | Some v ->
      match v.ToLowerInvariant() with
      | "t" | "true" -> true
      | x when x.StartsWith "-" -> true
      | _ -> false
    | None -> true

  match (Array.tryFindIndex matchName argv) with
  | Some i -> (Array.tryItem (i + 1) argv) |> parseValue
  | None -> false

let setJsonValue (prop: string, value: 'a) (json: JObject) =
  (json.Property prop).Replace (new JProperty (prop, value)) 

let execCmd (cmd: string) (args: string list) =
  let proc = new Process ()
  proc.StartInfo.CreateNoWindow <- true
  proc.StartInfo.FileName <-
    if OperatingSystem.IsWindows() then "cmd.exe"
    else "/bin/bash"
  proc.StartInfo.RedirectStandardInput <- true
  proc.StartInfo.RedirectStandardInput <- true
  proc.StartInfo.RedirectStandardOutput <- true
  proc.Start() |> ignore

  cmd :: args
  |> String.concat " "
  |> proc.StandardInput.WriteLine
  proc.StandardInput.Flush()
  proc.StandardInput.Close()
  proc.WaitForExit()

  {
    code = proc.ExitCode
    err = proc.StandardError.ReadToEnd()
    out = proc.StandardOutput.ReadToEnd()
  }

let writeFile (path: string) (contents: string) =
  File.WriteAllText(path, contents)
