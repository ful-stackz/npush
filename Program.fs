open Npush.Tools
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

let promptConfirm (text: string) =
    let prompt = new ConfirmationPrompt (text)
    prompt.DefaultValue <- true
    AnsiConsole.Prompt prompt

type LogLevel =
    | Debug = 0
    | Info = 1

type ProgramOptions = {
    dryRun: bool
    logLevel: LogLevel
    requireConfirmation: bool
}

[<EntryPoint>]
let main argv =
    let config = {
        dryRun = findFlag "dry" argv
        logLevel =
            match (findArg "log" argv) with
            | Some level ->
                match level.ToUpperInvariant() with
                | "DEBUG" -> LogLevel.Debug
                | _ -> LogLevel.Info
            | None -> LogLevel.Debug
        requireConfirmation = findFlag "confirm|c" argv
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
    log LogLevel.Info ""

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
    log LogLevel.Info ""

    if config.requireConfirmation then
        match promptConfirm "Proceed with update?" with
        | true -> log LogLevel.Info ""
        | false -> exit 0

    let logProcess (text: string) = log LogLevel.Info $"  ⏳ {text.EscapeMarkup()}..."
    let logDone (text: string) = log LogLevel.Info $"  ✅ {text.EscapeMarkup()}"
    let logErr (text: string) = log LogLevel.Info $"  ❌ {text.EscapeMarkup()}"

    logProcess "updating version in package.json"
    setJsonValue ("version", nextFullVersion) fileJson.Value
    writeFile filePath (fileJson.Value.ToString())
    logDone "package.json updated"

    if config.dryRun then
        // 1. Stage package.json
        logProcess "stage package.json"
        match git.add ["package.json"] with
        | Ok _ -> logDone "package.json staged"
        | Error e -> terminate $"Staging package.json failed! {e}" 1
 
        // 2. Create commit
        logProcess "commit package.json"
        match git.commit $"Bump version to {nextFullVersion}" with
        | Ok _ -> logDone "package.json committed"
        | Error e -> terminate $"Creating commit failed! {e}" 1

        // 3. Create tag
        logProcess "create tag"
        match git.tag $"v{nextFullVersion}" with
        | Ok _ -> logDone "tag created"
        | Error e -> terminate $"Creating tag failed! {e}" 1

        // 4. Npm publish
        logProcess "pack without publishing"
        let publishResult = npm.publish ["--dry-run"]
        match publishResult with
        | Ok _ -> logDone "packaging done"
        | Error e -> logErr $"packaging failed: {e}"

        // 5. Delete tag
        logProcess "delete tag"
        match git.tag $"-d v{nextFullVersion}" with
        | Ok _ -> logDone "tag deleted"
        | Error e -> logErr $"deleting tag failed: {e}"

        // 6. Revert commit
        logProcess "revert commit"
        match git.reset "mixed" "HEAD~1" with
        | Ok _ -> logDone "reverted update commit"
        | Error e -> terminate $"Reverting commit failed! {e}" 1

        match publishResult with
        | Ok res -> log LogLevel.Info $"\n{res.EscapeMarkup()}"
        | Error _ -> ()
    else
        // 1. Stage package.json
        logProcess "stage package.json"
        match git.add ["package.json"] with
        | Ok _ -> logDone "package.json staged"
        | Error e -> terminate $"Staging package.json failed! {e}" 1
 
        // 2. Create commit
        logProcess "commit package.json"
        match git.commit $"Bump version to {nextFullVersion}" with
        | Ok _ -> logDone "package.json committed"
        | Error e -> terminate $"Creating commit failed! {e}" 1

        // 3. Create tag
        logProcess "create tag"
        match git.tag $"v{nextFullVersion}" with
        | Ok _ -> logDone "tag created"
        | Error e -> terminate $"Creating tag failed! {e}" 1

        // 4. Npm publish
        logProcess "publish package to npm"
        let publishResult = npm.publish []
        match publishResult with
        | Ok _ -> logDone "package published"
        | Error _ ->
            logErr "publishing package failed"
            logProcess "revert version commit"
            match git.reset "mixed" "HEAD~1" with
            | Ok _ -> logDone "reverted update commit"
            | Error e -> terminate $"Reverting commit failed! {e}" 1

        // 5. Git push
        logProcess "push update to origin"
        match git.push "origin" with
        | Ok _ -> logDone "update pushed to origin repo"
        | Error e -> terminate $"Pushing update commit failed! {e}" 1

        match publishResult with
        | Ok res -> log LogLevel.Info $"\n{res.EscapeMarkup()}"
        | Error _ -> ()

    0 // return an integer exit code