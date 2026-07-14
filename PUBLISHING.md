# Publishing to NuGet

PromptRaster publishes two packages — `PromptRaster` and `PromptRaster.MicrosoftExtensionsAI` — from GitHub Actions when a version tag is pushed. The workflow is [`release.yml`](.github/workflows/release.yml).

## One-time setup (maintainers)

### 1. Create a NuGet API key

1. Sign in at [nuget.org](https://www.nuget.org)
2. Click your username → **API Keys** → **Create**
3. Scope the key to push for the `PromptRaster*` glob pattern

### 2. Add a GitHub repository secret

**Settings → Secrets and variables → Actions → New repository secret**

| Name | Value |
|------|-------|
| `NUGET_API_KEY` | The API key created above |

### 3. Publish the first version

1. Ensure the code on `main` is what you want to ship (CI green)
2. Create and push a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

3. Watch **Actions → Release** on GitHub
4. After validation, the packages appear at:
   - https://www.nuget.org/packages/PromptRaster
   - https://www.nuget.org/packages/PromptRaster.MicrosoftExtensionsAI

The workflow derives the package version from the tag (`v0.1.0` → `0.1.0`), so the `VersionPrefix` in `Directory.Build.props` is only a fallback for local builds.

## Releasing a new version

1. Merge the changes to `main` and wait for CI
2. Tag and push:

```bash
git tag v0.1.1
git push origin v0.1.1
```

Each tag triggers build → test → pack → push to NuGet → GitHub release with the `.nupkg` and `.snupkg` files attached.

## Notes

- Tags must match `v*` (e.g. `v0.1.0`, `v1.2.3`) and follow semantic versioning
- NuGet does not allow republishing the same version — bump the version for every release
- Releases never run from pull requests; only tag pushes trigger publishing
- Symbol packages (`.snupkg`) are pushed automatically alongside the main packages
- Package readme images must use an [allowlisted domain](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images) (e.g. `raw.githubusercontent.com`). Relative paths like `icon.png` do not render on nuget.org
