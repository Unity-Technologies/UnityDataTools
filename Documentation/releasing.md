# Releasing UnityDataTool

A release is a git tag plus a GitHub release with one zip per platform. The build and the upload are
automated: pushing a `vX.Y.Z` tag runs the "Build UnityDataTool" action, which publishes a **draft**
release with the zips and their checksums attached. What is left is the version bookkeeping around
the tag and writing the release notes.

## The version convention

`UnityDataTool/UnityDataTool.csproj` holds the version. On `main` the `InformationalVersion` always
carries a `-dev` suffix (for example `2.3.0-dev`), which is what `--version` reports and what makes
the documentation link in `--help` point at `main`. A release strips that suffix, so a tagged binary
reports a bare `2.3.0` and links to the docs as they were at the tag.

Immediately after tagging, `main` moves to the *next* `-dev` version. That way work can land on
`main` right after a release without landing on a version number that has already shipped.

## Steps

1. Check that the tests are green on `main`:
   <https://github.com/Unity-Technologies/UnityDataTools/actions>.

2. Drop the `-dev` suffix from `InformationalVersion`, commit as `Release vX.Y.Z`, then tag and push
   both:

   ```
   git tag vX.Y.Z
   git push origin main
   git push origin vX.Y.Z
   ```

3. Bump `main` to the next version: `AssemblyVersion` and `FileVersion` to `X.Y+1.0.0`,
   `InformationalVersion` to `X.Y+1.0-dev`. Commit and push.

4. The tag push builds `UnityDataTool-windows-x64.zip`, `UnityDataTool-macos-arm64.zip` and
   `UnityDataTool-linux-x64.zip`, writes `checksums.txt`, and creates the draft release. Confirm all
   four assets arrived:

   ```
   gh release view vX.Y.Z
   ```

5. Write the notes and publish. The generated notes list the merged PRs; a short summary of the
   user-visible changes on top of them is what most readers actually read.

   ```
   gh release edit vX.Y.Z --title "vX.Y.Z <short description>" --draft=false
   ```

Steps 2 and 3 are mechanical and worth scripting if you cut releases often.

## Why the asset names have no version in them

`https://github.com/Unity-Technologies/UnityDataTools/releases/latest/download/UnityDataTool-windows-x64.zip`
only works as a permanent download link while the asset names stay the same from release to release.
The README and the agent guide hand those URLs to users, so the names should not be changed or have
the version added back into them. The version is in the release title, in `checksums.txt`, and in
`--version`.

Re-running the action for a tag that already has a release re-uploads the assets over the existing
ones, so a failed or incomplete run can simply be re-run.
