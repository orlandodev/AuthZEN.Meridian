# Third-Party Notices

This repository is licensed under MIT (see `LICENSE`). That covers the code in
this repository only. Several dependencies carry their own, separate license
terms — most importantly:

## Duende IdentityServer

`Identity/Meridian.IdentityServer` depends on `Duende.IdentityServer`, which is
**not** MIT-licensed. Duende IdentityServer is free to use for development,
testing, and by qualifying small companies (revenue/funding under Duende's
published threshold — check their current terms), but requires a **paid
license for production use** beyond that. This applies regardless of the
license on this repository. If you build on Meridian for anything beyond a
demo or internal learning exercise, check https://duendesoftware.com/products/identityserver
for current terms before deploying.

## Stage 5+ policy engines

OPA (Open Policy Agent) and OpenFGA, introduced in Stage 5, are both licensed
under Apache-2.0 — no similar production-licensing caveat applies to them.

## Everything else

Standard NuGet/npm dependencies (ASP.NET Core, EF Core, OpenTelemetry, etc.)
are used under their own respective open-source licenses (mostly MIT/Apache-2.0).
This file calls out Duende specifically because it's the one dependency in
this solution with a licensing model that differs materially from "free for
any use."
