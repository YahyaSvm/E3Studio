# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.1.x   | Yes       |

## Reporting a Vulnerability

We take the security of E3Studio seriously. If you discover a security vulnerability, please follow these steps:

### Private Disclosure

**Do NOT open a public issue for security vulnerabilities.**

Instead, please report it privately:

1. Go to [Security Advisories](https://github.com/yahyasvm/E3Studio/security/advisories)
2. Click "Report a vulnerability"
3. Fill in the details of the vulnerability

Alternatively, you can contact the maintainer directly through GitHub.

### What to Include

- **Description** — Clear description of the vulnerability
- **Steps to reproduce** — If applicable
- **Impact** — What an attacker could achieve
- **Suggested fix** — If you have one
- **CVSS score** — If known

### Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial assessment**: Within 1 week
- **Fix development**: Depending on severity and complexity
- **Disclosure**: After fix is released, with credit to the reporter (unless anonymous)

## Security Best Practices

### For Users

- Always verify G-Code output before running on your CNC machine
- Do not run E3Studio backend with elevated privileges
- Keep your dependencies updated
- Use the latest release version

### For Developers

- Never commit secrets, keys, or credentials
- Use parameterized queries for any data storage
- Validate all input from WebSocket API
- Sanitize file paths to prevent directory traversal
- Follow the principle of least privilege
- Report any security concerns in code review

## Dependency Security

E3Studio uses the following dependency management:

| Component | Manager | Audit Command |
|-----------|---------|---------------|
| C++ libraries | vcpkg | `vcpkg list` |
| .NET packages | NuGet | `dotnet list package --vulnerable` |
| Node.js packages | npm | `npm audit` |

### Automated Scanning

GitHub Dependabot is configured to monitor dependencies and create PRs for security updates.

## G-Code Safety

**IMPORTANT**: E3Studio generates G-Code for CNC machines. Always:

1. **Simulate first** — Use the built-in simulation before running on a machine
2. **Verify coordinates** — Check that all coordinates are within your machine's limits
3. **Check feed rates** — Ensure feed rates are appropriate for your material and tool
4. **Test on soft material** — Run the first job on foam or wood to verify
5. **Keep emergency stop accessible** — Always have E-stop within reach

The authors are not responsible for any damage or injury resulting from the use of generated G-Code.
