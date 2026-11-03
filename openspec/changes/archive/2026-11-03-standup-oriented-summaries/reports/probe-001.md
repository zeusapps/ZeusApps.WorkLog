# Wording probe 001: fresh agents with the new guidance

Orchestrator-run evaluation at the user's request (2026-11-03), on the current target `head=6d23796 baseline=6d23796 files=11 diff=28b2dff190cb untracked=2/789d00f904e0`.

## Method

- Two fresh Claude Code general-purpose subagents (Claude Code 2.1.317), no conversation context: one on the default model (claude-opus-5-5), one on claude-sonnet-5-5. No tools used; the agents answered from the prompt only.
- The prompt gave each agent the exact texts a client sees with this change: the rendered managed instruction block (summary sentence and digest paragraph from `LoggingGuidance`), the `log_work` `summary` and `save_digest` `text` descriptions, and the digest request `instruction` (801 characters, matching analysis-001's measurement).
- Synthetic, process-heavy session memory and recorded summaries (no real journal content): a finished "mute voice notifications while the microphone is in use" feature with delegation, report IDs, test and task counts, a commit hash, archive and merge; and an in-progress hook shell fix with test counts, hook migration, a dev-build install and a blocker needing a team decision.
- Steps: (1) log the done observation; (2) save the digest for four observations, three of them old-style process-heavy summaries; (3) log an in-progress observation for the second item.
- The agents were not told what was being evaluated.
- Not covered: Codex models, the real MCP round-trip, and the managed block as loaded by a client from disk. Unit tests cover delivery of the text to every surface for both clients.

## Results

| Step | claude-opus-5-5 | claude-sonnet-5-5 |
| --- | --- | --- |
| 1 done summary | "Voice notifications now mute while another app uses the mic and resume after, confirmed in a real call. Exclusive-mode and remote audio are excluded." | "Spoken notifications mute while another app uses the mic and resume after; exclusive-mode and remote-desktop capture not detected." |
| 2 digest | "Finished voice muting during mic use: spoken notifications now pause while another app uses the microphone (calls, dictation) and resume afterward, still showing visually; confirmed in a real call. Exclusive-mode capture and remote-desktop audio are out of scope." | "Spoken notifications now stay muted while another app uses the microphone and resume after the call, with visual notifications unchanged; confirmed in a real call. Exclusive-mode and remote-desktop audio are out of scope. Finished." |
| 3 in-progress summary | "Hooks now pick a shell Codex can run, so they fire under PowerShell 7. The cmd fallback is unproven, and the team must decide on a 300 ms budget (now ~385 ms)." | "Hooks now work with PowerShell 7 in Claude and Codex; cmd fallback still unproven, team must decide 300 ms latency budget (~385 ms now)." |

Both digests passed exactly the four listed IDs and stayed within 280 characters.

## Assessment

- Passed for both models: no output mentions any excluded mechanic. Every output states the outcome in feature terms, keeps the accepted scope limit, and step 3 keeps the blocker that needs the team. The outputs are comparable to design.md's reference examples.
- Minor: summaries run about 130 to 160 characters against "about 140", and the opus step 3 summary uses two sentences. Length remains soft guidance; enforcement is out of scope.
