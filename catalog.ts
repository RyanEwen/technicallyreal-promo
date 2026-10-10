/** Public catalog contract shared by web consumers. Legacy Windows fields stay intact. */
export interface AppLink {
  platform: 'Android' | 'Windows' | 'Web'
  url: string
  label: string
}

export interface CatalogApp {
  appId: string
  name: string
  blurb: string
  icon: string
  links: AppLink[]
}

export const CATALOG_BASE_URL = 'https://raw.githubusercontent.com/RyanEwen/technicallyreal-promo/main/Assets/PromotedApps/'

/** Validate untrusted remote or cached data before displaying it or following links. */
export function parseCatalog(value: unknown): CatalogApp[] {
  if (!value || typeof value !== 'object' || !('apps' in value) || !Array.isArray(value.apps)) {
    throw new Error('Invalid app catalog')
  }

  const ids = new Set<string>()
  return value.apps.map((app: unknown) => {
    if (!app || typeof app !== 'object') throw new Error('Invalid catalog app')
    const entry = app as Record<string, unknown>
    for (const key of ['appId', 'name', 'blurb', 'icon']) {
      if (typeof entry[key] !== 'string' || !entry[key]) throw new Error(`Invalid ${key}`)
    }
    const { appId, name, blurb, icon } = entry as Record<string, string>
    if (!appId || !name || !blurb || !icon || !/^[\w-]+\.png$/.test(icon) || ids.has(appId)) {
      throw new Error('Invalid app identity or icon')
    }
    ids.add(appId)
    if (!Array.isArray(entry.links) || entry.links.length === 0) throw new Error('Missing app links')

    const links = entry.links.map((link: unknown): AppLink => {
      if (!link || typeof link !== 'object') throw new Error('Invalid app link')
      const fields = link as Record<string, unknown>
      if (fields.platform !== 'Android' && fields.platform !== 'Windows' && fields.platform !== 'Web') {
        throw new Error('Invalid app platform')
      }
      if (typeof fields.url !== 'string' || typeof fields.label !== 'string' || !fields.label) {
        throw new Error('Invalid app destination')
      }
      const url = new URL(fields.url)
      if (url.protocol !== 'https:' || url.username || url.password) throw new Error('Unsafe app destination')
      return { platform: fields.platform, url: url.href, label: fields.label }
    })

    return { appId, name, blurb, icon, links }
  })
}
