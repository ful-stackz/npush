module Npush.Tools

open Npush.Utilities
open Spectre.Console

module npm =
  let publish (args: string list) =
    let result = execCmd "npm" ("publish" :: args)
    if result.code <> 0
    then Error $"npm publish exited with code {result.code}"
    else Ok $"{result.stderr}\n{result.stdout}"

module git =
  let tag (name: string) =
    let result = execCmd "git" [ "tag"; name ]
    if result.code <> 0
    then Error $"git tag exited with code {result.code}"
    else Ok ""

  let add (files: string list) =
    let result = execCmd "git" ("add" :: files)
    if result.code <> 0
    then Error $"git add exited with code {result.code}"
    else Ok ""

  let commit (message: string) =
    let result = execCmd "git" [ "commit"; "-m"; $"\"{message}\"" ]
    if result.code <> 0
    then Error $"git commit exited with code {result.code}"
    else Ok ""

  let push (target: string) =
    let result = execCmd "git" [ "push"; target ]
    if result.code <> 0
    then Error $"git push exited with code {result.code}"
    else Ok ""

  let reset (mode: string) (target: string) =
    let result = execCmd "git" [ $"--{mode}"; target ]
    if result.code <> 0
    then Error $"git reset exited with code {result.code}"
    else Ok ""
