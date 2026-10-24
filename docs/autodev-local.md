# Local AutoDev MVP

This document tracks a separately opt-in, locally executed Codex CLI workflow authenticated with ChatGPT rather than a metered OpenAI API key.

The implementation prototype and full guide are supplied in the associated ChatGPT conversation as `local-autodev-mvp.zip`. Do not apply the `autodev` label: that triggers the existing separately-billed API-key GitHub Actions workflow. Use a distinct `local-autodev` label only after reviewing local authentication, usage allowance and purchased-credits settings.

The prototype is deliberately one-shot, read-only during AI generation, checks patches using `.github/scripts/patch_guard.py`, proposes a Draft PR and never merges automatically. End-to-end execution is not yet verified.
