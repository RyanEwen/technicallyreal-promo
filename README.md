# technicallyreal-promo

Shared "Our other apps" page for TechnicallyReal's WinUI 3 apps. Each app bundles this
as a git submodule; the page cross-promotes the publisher's other Microsoft Store apps
and automatically hides the app it is running inside (matched by package family name).

**No runtime networking.** All Store data (names, blurbs, icons) is fetched at
development time by `refresh.ps1` and committed here as static assets. Consuming apps
ship those assets in their package; the only "Store" interaction at runtime is
launching the Store app via `ms-windows-store://pdp/` when the user clicks a card.

## Consuming (per app, one-time)

```powershell
git submodule add https://github.com/RyanEwen/technicallyreal-promo external/promo
```

In the app's `.csproj`:

```xml
<Import Project="external\promo\PromotedApps.projitems" Label="Shared" />
```

Add a nav item (e.g. in `NavigationView.FooterMenuItems`) that navigates to
`TechnicallyReal.Promo.PromotedAppsPage`.

CI note: checkouts need submodules, e.g. `actions/checkout` with `submodules: true`.

## Updating the promoted apps

1. Add/remove product IDs in `ids.json` (optionally set a `blurbOverrides` entry to
   replace the auto-extracted first line of the Store description).
2. Run `.\refresh.ps1` - it queries the Store's StoreEdgeFD endpoint and regenerates
   `Assets/PromotedApps/apps.json` plus the icon files.
3. Review the diff, commit, then bump the submodule pointer in each app with its next
   release (`git submodule update --remote external/promo`).

`refresh.ps1` is the only thing that talks to the Store backend, and it runs on a dev
machine - if Microsoft changes the (unofficial) endpoint, shipped apps are unaffected.
