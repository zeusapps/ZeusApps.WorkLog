# Release operations

The public repository is [zeusapps/ZeusApps.WorkLog](https://github.com/zeusapps/ZeusApps.WorkLog), owned by the personal account `zeusapps`. Its primary branch is `main`. Source publication and tagging are deliberate maintenance operations; running the application does neither.

For initial publication, verify the authenticated GitHub account and target repository, review outgoing tracked files/history, create the public repository if absent, add its origin, and push main without rewriting history. Keep private client configuration, journals, caches, backups, and ignored artifacts local. An initialized checkout retains its saved local repository identity when origin is added; a new checkout can derive the GitHub identity. Existing journal observations are not automatically renamed or regrouped.

For each release:

1. Set the intended unpublished `MAJOR.MINOR.PATCH` in Directory.Build.props. Do not reuse or move a published tag.
2. Run locked restore, Release build/test, scripts/package.ps1, and the packaged update acceptance check:

   ```powershell
   dotnet run --project tools/WorkLog.Smoke -c Release --no-build -- --updates-only artifacts/releases/worklog-VERSION-win-x64/worklog.exe
   ```

3. Commit and push main. Observe the GitHub validation workflow and resolve failures.
4. Push its matching `vMAJOR.MINOR.PATCH` tag. The release workflow independently requires successful Windows validation and rejects tag/application version mismatches.
5. Observe the publish job. Verify that the public release contains `worklog-VERSION-win-x64.zip` and its `.sha256` file, download them independently, compare the hash, and check the extracted executable version/MCP startup against isolated data.
6. Verify the public latest-release endpoint returns the intended stable version and equal installed/published versions produce no update hint. Record the release link and outcome; required pending validation means release work is still in progress.

The first planned release is `v0.1.0` if still unpublished. The application update feed uses the public latest stable release, excludes drafts/prereleases, and requires both the official Windows ZIP and checksum. Full agent-client smoke checks remain available locally; optional `--live-agents` checks require an explicit decision to send authenticated test prompts.
