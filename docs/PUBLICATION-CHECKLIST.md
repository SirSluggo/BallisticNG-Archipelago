# Prerelease preparation

The intended repository name is **BallisticNG-Archipelago**. No GitHub repository or Discord post has been created by this preparation.

## Prepared

- Original code under the owner-selected MIT license; Harmony notice retained.
- Matching v0.36.1 source and a build script that produces player/source archives with a hash manifest and parity checks.
- Plain-text player instructions, README, architecture/support/testing docs, known issues and bug-report form.
- Extensive AI-use disclosure, including text suitable for a pinned post.
- Runtime data and game assets excluded from the source package.
- Consistent project naming and concise player/YAML instructions.

## In progress

- Full-seed regression on the current build.
- Idle-demo crash investigation. Twenty demos without the AP mod did not reproduce it; a cause or fix is not established.

## Owner review before sharing

1. Read the source and support instructions, especially save ownership, file writes, state cleanup and failure handling. Ask for independent human review where available; do not imply it has happened.
2. Decide whether to share a limited development prerelease while the demo crash is open. Keep the crash and unfinished full-seed test prominent.
3. Select the GitHub account/organization and support route. The repository name is agreed; the owner/account has not been supplied here.
4. Compare the release and source tag. Use the generated player/source ZIPs as release assets; keep dist/ out of source control.
5. Review the disclosure against the community's current rules and pin it where required. A disclosure alone is not a claim of permission or moderator acceptance.

Do not call the implementation stable, independently reviewed, Linux-compatible or generally Cloud-safe on the present evidence. The owner's willingness to maintain it and ability to troubleshoot should be represented honestly.
