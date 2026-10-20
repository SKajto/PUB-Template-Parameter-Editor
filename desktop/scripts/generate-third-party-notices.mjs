import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDirectory = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const lock = JSON.parse(
  readFileSync(join(projectDirectory, 'package-lock.json'), 'utf8'),
);

const components = [
  {
    package: 'electron',
    name: 'Electron',
    licensePath: 'node_modules/electron/LICENSE',
  },
  {
    package: 'crc-32',
    name: 'crc-32',
    licensePath: 'node_modules/crc-32/LICENSE',
  },
  {
    package: 'react',
    name: 'React',
    licensePath: 'node_modules/react/LICENSE',
  },
  {
    package: 'react-dom',
    name: 'React DOM',
    licensePath: 'node_modules/react-dom/LICENSE',
  },
  {
    package: 'scheduler',
    name: 'scheduler',
    lockPath: 'node_modules/react-dom/node_modules/scheduler',
    licensePath: 'node_modules/react-dom/node_modules/scheduler/LICENSE',
  },
  {
    package: 'saxes',
    name: 'saxes',
    licensePath: 'licenses/saxes-LICENSE.txt',
  },
  {
    package: 'xmlchars',
    name: 'xmlchars',
    licensePath: 'node_modules/xmlchars/LICENSE',
  },
  {
    package: 'yauzl',
    name: 'yauzl',
    licensePath: 'node_modules/yauzl/LICENSE',
  },
  { package: 'pend', name: 'pend', licensePath: 'node_modules/pend/LICENSE' },
];

const runtimeLibraries = components.map((component) => {
  const packagePath = component.lockPath ?? `node_modules/${component.package}`;
  const lockEntry = lock.packages[packagePath];
  if (!lockEntry) {
    throw new Error(`Missing lockfile entry for ${component.package}`);
  }
  const license = lockEntry.license;
  if (typeof license !== 'string' || license.length === 0) {
    throw new Error(`Missing SPDX license metadata for ${component.package}`);
  }
  const licenseText = readFileSync(
    join(projectDirectory, component.licensePath),
    'utf8',
  ).trim();
  const manifest = JSON.parse(
    readFileSync(join(projectDirectory, packagePath, 'package.json'), 'utf8'),
  );
  const repository =
    typeof manifest.repository === 'string'
      ? manifest.repository
      : manifest.repository?.url;

  return {
    name: component.name,
    version: lockEntry.version,
    license,
    repository: repository ?? '',
    licenseText,
  };
});

const notices = [
  'THIRD-PARTY SOFTWARE NOTICES',
  '============================',
  '',
  'This file contains license texts and attribution notices for runtime components bundled with PUB Template Parameter Editor.',
  'Electron also distributes Chromium notices in LICENSES.chromium.html alongside the application executable.',
  '',
  ...runtimeLibraries.flatMap((component) => [
    '--------------------------------------------------------------------------------',
    `${component.name} ${component.version} — ${component.license}`,
    ...(component.repository ? [`Source: ${component.repository}`] : []),
    '',
    component.licenseText,
    '',
  ]),
]
  .join('\n')
  .trimEnd();

writeFileSync(
  join(projectDirectory, 'THIRD_PARTY_NOTICES.txt'),
  `${notices}\n`,
  'utf8',
);
writeFileSync(
  join(projectDirectory, 'src', 'runtime-libraries.json'),
  `${JSON.stringify(
    runtimeLibraries.map(({ name, version, license }) => ({
      name,
      version,
      license,
    })),
    null,
    2,
  )}\n`,
  'utf8',
);
console.log(
  `Generated notices for ${runtimeLibraries.length} runtime components.`,
);
