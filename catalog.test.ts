import assert from 'node:assert/strict'
import { test } from 'node:test'
import manifest from './Assets/PromotedApps/apps.json'
import { parseCatalog } from './catalog.js'

test('the bundled catalog has stable identities and browser-safe platform links', () => {
  const apps = parseCatalog(manifest)
  assert.equal(apps.length, 5)
  assert.deepEqual(apps.map((app) => app.name), [
    'PrintStream', 'Persistent', 'Little Launcher', 'ImmichDrive', 'Repilot'
  ])
  assert.deepEqual(apps.find((app) => app.appId === 'repilot')?.links.map((link) => link.platform), ['Windows'])
  assert.deepEqual(apps.find((app) => app.appId === 'printstream')?.links.map((link) => link.platform), ['Android', 'Windows', 'Web'])
  assert.equal(apps.filter((app) => app.appId !== 'persistent').length, 4)
  assert.ok(apps.find((app) => app.appId === 'printstream')?.blurb.includes('3D printers'))
  assert.ok(apps.find((app) => app.appId === 'drive-for-immich')?.blurb.startsWith('ImmichDrive'))
})

test('rejects unsafe destinations, icon paths, duplicate ids and old catalog shapes', () => {
  for (const url of ['javascript:alert(1)', 'ms-windows-store://pdp/', 'http://example.com', 'https://user:password@example.com']) {
    const value = structuredClone(manifest)
    value.apps[0]!.links[0]!.url = url
    assert.throws(() => parseCatalog(value))
  }
  const badIcon = structuredClone(manifest)
  badIcon.apps[0]!.icon = '../secret.png'
  assert.throws(() => parseCatalog(badIcon))
  assert.throws(() => parseCatalog({ apps: [manifest.apps[0], manifest.apps[0]] }))
  assert.throws(() => parseCatalog({ apps: [{ name: 'Old catalog' }] }))
  assert.deepEqual(parseCatalog({ apps: [] }), [])
})
