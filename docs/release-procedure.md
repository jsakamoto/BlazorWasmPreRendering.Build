# Release procedure

This document describes how a new version of the `BlazorWasmPreRendering.Build` package is released. It was written down from the release commits in the git history (for example, `01d679b Bump version to 10.0.1 for release`). The last section lists the steps that the history does not show.

## Where the version lives

| File | What to change |
| --- | --- |
| `VersionInfo.props` | `<Version>` is the package version. |
| `RELEASE-NOTES.txt` | The newest entry at the top becomes the release notes of the package. |
| `SampleApps/BlazorWasmApp0/BlazorWasmApp0.csproj` | The version of the `BlazorWasmPreRendering.Build` package reference. |
| `SampleApps/BlazorWasmApp1/BlazorWasmApp1.csproj` | Same as above. |
| `SampleApps/BlazorWasmAVP/BlazorWasmAVP.csproj` | Same as above. |
| `SampleApps/BlazorWasmLazyLoading/BlazorWasmApp/BlazorWasmApp.csproj` | Same as above. |

The sample apps must reference the new version, because the E2E tests publish them with the package that the Release build puts into `_dist/`. To find every reference, search the `.csproj` files under `SampleApps/` for `Include="BlazorWasmPreRendering.Build"`.

## Steps

1. **Start from an up to date `master`** with no uncommitted changes.

2. **Add an entry to `RELEASE-NOTES.txt`.** Put it at the top, in the same format as the existing entries.

   ```text
   v.10.0.2
   - Fix: Resolved an issue where ...
   - Improve: ...

   v.10.0.1
   - ...
   ```

   Each line starts with `Fix:`, `Improve:`, or `Feature:`, and ends with the issue or PR number in parentheses when there is one. Write for package users, not for developers of this repository.

3. **Update `<Version>` in `VersionInfo.props`.**

4. **Update the package reference version in the sample apps** listed above to the same number.

5. **Build the package.**

   ```shell
   dotnet build BlazorWasmPreRendering.Build -c Release
   ```

   This publishes the build program and the WebHost for every target framework, then packs `_dist/BlazorWasmPreRendering.Build.<version>.nupkg`. Check that the file exists.

   If a package with the same version was restored before (for example, from an earlier try), delete `~/.nuget/packages/blazorwasmprerendering.build/<version>` first. Otherwise the tests use the old one from the cache.

6. **Run all the tests.**

   ```shell
   dotnet test BlazorWasmPreRendering.Build.Test
   ```

   Many tests are slow E2E tests that publish the sample apps with the new package. All of them must pass. One test (`Publish_by_native_msbuild_Test`) is skipped when Visual Studio is not set up for it.

7. **Commit the changes** with the message `Bump version to <version> for release`.

8. **Tag the commit** with a lightweight tag named `v.<version>`, such as `v.10.0.2`.

   ```shell
   git tag v.10.0.2
   ```

9. **Push the commit and the tag.**

   ```shell
   git push origin master
   git push origin v.10.0.2
   ```

   A push to `master` also deploys `SampleApps/BlazorWasmApp1` to GitHub Pages through `.github/workflows/gh-pages.yml`.

10. **Publish the package to nuget.org.** See the next section.

## Steps that the history does not show

The repository has no workflow that publishes the package, so the following is done by hand. The details are not recorded here.

- **Pushing the package to nuget.org.** For example, upload `_dist/BlazorWasmPreRendering.Build.<version>.nupkg` on nuget.org, or run `dotnet nuget push` with an API key.
- **Creating a GitHub release** for the tag, if one is made.

Fill in this section when the actual steps are known.

## Preview releases

Some past releases were previews, such as `v.8.1.0-preview.6` and `v.11.0.0-rc.1`. They were committed as `Release v.<version> with ...` or `v.<version> release`, on branches other than `master`. Their exact steps are not covered here.
