# FSA-AI01

## Metadata
- **Severity:** Major
- **Message:** Unvalidated AI output. No smart constructor on AI result.
- **Related Rules:** FSA-AI07

## Explanation
AI outputs (e.g., from OpenAI, Anthropic) are untrusted. They must be validated through a smart constructor before entering the domain.
