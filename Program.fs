open Npush.Types
open Npush.Utilities
open Spectre.Console

[<Literal>]
let logo = """
     __ _  ____  _  _  ____  _  _ 
    (  ( \(  _ \/ )( \/ ___)/ )( \
    /    / ) __/) \/ (\___ \) __ (
    \_)__)(__)  \____/(____/\_)(_/
"""

let promptUpdateType (current: SemVer) =
    let prompt = new SelectionPrompt<UpdateType> ()
    prompt.Title <- "Pick next version:"
    prompt.Choices.AddRange [ UpdateType.Patch; UpdateType.Minor; UpdateType.Major ]
    prompt.Converter <- fun t ->
        match t with
        | UpdateType.Major -> $"{current.major + 1}.0.0 (Major update)"
        | UpdateType.Minor -> $"{current.major}.{current.minor + 1}.0 (Minor update)"
        | UpdateType.Patch -> $"{current.major}.{current.minor}.{current.patch + 1} (Patch update)"
    AnsiConsole.Prompt prompt

let promptNextStage (version: SemVer) =
    let semVer = fSemVer version
    let prompt = new SelectionPrompt<Stage> ()
    prompt.Title <- "Pick version flag:"
    prompt.Choices.AddRange [ Stage.Release; Stage.Preview; Stage.Beta; Stage.Alpha ]
    prompt.Converter <- fun t ->
        match t with
        | Stage.Release -> $"{semVer}"
        | Stage.Preview -> $"{semVer}-preview"
        | Stage.Beta -> $"{semVer}-beta"
        | Stage.Alpha -> $"{semVer}-alpha"
    AnsiConsole.Prompt prompt

type LogLevel =
    | Debug = 0
    | Info = 1

type ProgramOptions = {
    dryRun: bool
    test: bool
    logLevel: LogLevel
}

[<EntryPoint>]
let main argv =
    let config = {
        dryRun = findFlag "dry" argv
        test = findFlag "test" argv
        logLevel =
            match (findArg "log" argv) with
            | Some level ->
                match level.ToUpperInvariant() with
                | "DEBUG" -> LogLevel.Debug
                | _ -> LogLevel.Info
            | None -> LogLevel.Debug
    }
 
    let log (level: LogLevel) (text: string) =
        if level >= config.logLevel then
            AnsiConsole.MarkupLine text

    printfn "%s" logo

    let filePath = getFilePath "package.json"
    let fileContents = getFileContents filePath
    let fileJson =
        match fileContents with
        | Some contents -> parseJson contents
        | _ -> terminate "Could not find package.json in the current directory" 1
    let package =
        match fileJson with
        | Some json ->
            match asPackageJson json with
            | Ok package -> package
            | Error error -> terminate error 1
        | _ -> terminate "package.json is not a valid JSON file" 1

    if config.dryRun
    then log LogLevel.Info "⚠️  Running in dry mode"
    log LogLevel.Info $"📦 Loaded package.json: {filePath}"
    log LogLevel.Info $"✏️  Package name: [b]{package.name}[/]"
    log LogLevel.Info $"#️⃣  Package version: [b]{fVer package.version.semver package.version.stage}[/]"

    let updateTypeArg = findArg "update|u" argv
    let nextStageArg = findArg "stage|s" argv

    let updateType =
        match updateTypeArg with
        | Some "major" -> UpdateType.Major
        | Some "minor" -> UpdateType.Minor
        | Some "patch" -> UpdateType.Patch
        | _ -> promptUpdateType package.version.semver

    let nextVersion =
        match updateType with
        | UpdateType.Major -> {
            major = package.version.semver.major + 1
            minor = 0
            patch = 0 }
        | UpdateType.Minor -> {
            major = package.version.semver.major
            minor = package.version.semver.minor + 1
            patch = 0 }
        | UpdateType.Patch -> {
            major = package.version.semver.major
            minor = package.version.semver.minor
            patch = package.version.semver.patch + 1 }

    log LogLevel.Info $"Next version: [b]{fSemVer nextVersion}[/]"

    let nextStage =
        match nextStageArg with
        | Some "release" -> Stage.Release
        | Some ("rc" | "preview") -> Stage.Preview
        | Some "beta" -> Stage.Beta
        | Some "alpha" -> Stage.Alpha
        | _ -> promptNextStage nextVersion

    if nextStage <> Stage.Release
    then log LogLevel.Info $"Release stage: [b]{fStage nextStage}[/]"

    let nextFullVersion = fVer nextVersion nextStage
    log LogLevel.Info $"Next version will be: [b]{nextFullVersion}[/]"

    setJsonValue ("version", nextFullVersion) fileJson.Value
    writeFile filePath (fileJson.Value.ToString())

    if config.test then
        writeFile filePath (fileJson.Value.ToString())
    elif config.dryRun then
        log LogLevel.Info $"Performing [b]dry run[/]..."
        log LogLevel.Debug $"Running 'npm publish --dry-run'"
        execCmd "npm" [ "publish"; "--dry-run" ]
        log LogLevel.Debug $"Restoring package.json version..."
        setJsonValue ("version", fVer package.version.semver package.version.stage) fileJson.Value
        writeFile filePath (fileJson.Value.ToString())
    else
        log LogLevel.Info "📦 Publishing package..."
        writeFile filePath (fileJson.Value.ToString())

    0 // return an integer exit code