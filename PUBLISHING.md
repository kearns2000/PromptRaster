# Publishing to NuGet

PromptRaster publishes two packages - `PromptRaster` and `PromptRaster.MicrosoftExtensionsAI` - from GitHub Actions when a version tag is pushed. The workflow is [`ci.yml`](.github/workflows/ci.yml).

Publishing uses [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing). No long-lived API keys are stored in the repo.

## One-time setup (maintainers)

### 1. Create a trusted publishing policy on nuget.org

1. Sign in at [nuget.org](https://www.nuget.org)
2. Click your username → **Trusted Publishing**
3. **Add new policy** with:

| Field | Value |
|-------|-------|
| Policy name | `promptraster` (or any label) |
| Package owner | Your nuget.org account |
| Repository owner | `kearns2000` |
| Repository | `PromptRaster` |
| Workflow file | `ci.yml` |
| Environment | *(leave empty)* |

The workflow file must be exactly `ci.yml` - not the full path.

Docs: [Trusted Publishing on Microsoft Learn](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)

### 2. Add a GitHub repository secret

**Settings → Secrets and variables → Actions → New repository secret**

| Name | Value |
|------|-------|
| `NUGET_USER` | Your **nuget.org username** (profile name, not your email) |

Trusted publishing still needs your NuGet username for the login step; the temporary API key comes from OIDC.

You do **not** need a `NUGET_API_KEY` secret.

### 3. Publish the first version

1. Ensure `ci.yml` is on `main`
2. Create and push a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

3. Watch **Actions → ci** on GitHub
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

Each tag triggers build → test → pack → trusted publish → GitHub release with the `.nupkg` and `.snupkg` files attached.

## Notes

- Tags must match `v*` (e.g. `v0.1.0`, `v1.2.3`) and follow semantic versioning
- NuGet does not allow republishing the same version - bump the version for every release
- Releases never run from pull requests; only tag pushes trigger publishing
- The temporary API key from `NuGet/login@v1` expires in about an hour
- Do **not** store a `NUGET_API_KEY` secret - trusted publishing replaces that
- Package readme images must use an [allowlisted domain](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images) (e.g. `raw.githubusercontent.com`). Relative paths like `icon.png` do not render on nuget.org
