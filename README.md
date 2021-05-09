# npush

> Bump the version and publish your NPM packages with ease!

`npush` is first and foremost a (self) educational project. That being said it is still functional, but not at the level of already existing packages,
such as [np](https://www.npmjs.com/package/np).

Currently `npush` does the following:

1. Updates the version in `package.json`, adhering to [semantic versioning](https://semver.org)
2. Creates a version bump commit
3. Creates a version tag
4. Publishes the package to npm
5. Pushes the commit to origin

## Installation

`npush` is currently not available from any package registry, so you'll have to install it manually.

```bash
# 1. clone the repository
git clone https://github.com/ful-stackz/npush.git

# 2. cd into directory
cd npush

# 3. pack into a package
dotnet pack -c Release

# 4. install as a global tool
dotnet tool install --global --add-source ./bin/Release npush

# to update your global tool do step 3. and then
dotnet tool update --global --add-source ./bin/Release npush
```

## Usage

> Note that `npush` assumes you have `git` and `npm` available in the global `PATH`.

For the most basic usage simply run `npush` in your NPM package's directory. The app will prompt a couple of questions about the update and then
bump the version, create a commit, publish and push to the repo origin.

You can also provide the required configuration as command line arguments and skip the UI part. Consult with the arguments table below for required arguments.

### Arguments

> Arguments are also accessible via `npush -h|--help`

| Name | Values | Default | Description |
| :--- | :----- | :------ | :---------- |
| `--update` or `-u` | `major`, `minor` or `patch` | Required | Specify the update type. See [update types](#update-types) |
| `--stage` or `-s` | `release`, `preview`, `beta` or `alpha` | Required | Specify the release stage. See [release stages](#release-stages) |
| `--dry` | `true`, `t`, `false` or `f` | `false` | Run `npush` in dry mode. It will perform all operations as normal, except the package will not be published and the local area will be reverted at the end. **Note** that `npush` will perform a `mixed` reset instead of a `hard` one in order to prevent losing any local unstaged/uncommitted changes. |
| `--log` | `info` or `debug` | `info` | Specify the log level. |
| `--confirm` or `-c` | `true`, `t`, `false` or `f` | `false` | Specify whether a confirmation is required before starting the update and publish process. |

#### Update types

`npush` works with the three most common SemVer update types:

- major - `1.2.3` -> `2.0.0`; an update introducing major, breaking changes
- minor - `1.2.3` -> `1.3.0`; an update introducing non-breaking updates
- patch - `1.2.3` -> `1.2.4`; an update usually delivering bug fixes

#### Release stages

`npush` works with the four most common release stages and appends a nice suffix to the version to indicate it:

- release - `1.2.3`; major release, no suffix
- preview - `1.2.3-preview`; also known as acceptance or release candidate (rc)
- beta - `1.2.3-beta`
- alpha - `1.2.3-alpha`
