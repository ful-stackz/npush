module Npush.Types

type SemVer = {
  major: int
  minor: int
  patch: int
}

[<RequireQualifiedAccessAttribute>]
type Stage =
  | Release
  | Preview
  | Beta
  | Alpha

[<RequireQualifiedAccessAttribute>]
type UpdateType =
  | Major
  | Minor
  | Patch

type PackageVersion = {
  semver: SemVer
  stage: Stage
}

type PackageJson = {
  name: string
  version: PackageVersion
}

type CommandResult = {
  code: int
  stdout: string
  stderr: string
}
