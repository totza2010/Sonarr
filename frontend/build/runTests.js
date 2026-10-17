// Runs the UI's unit tests: plain TypeScript compiled by the project's own compiler and run by
// Node's built-in test runner, so nothing is added to the dependencies. The files under test have
// to stand on their own - importing only types from the rest of the app - because the compiled
// output does not know the app's import paths.
const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '../..');
const outDir = path.join(root, '_temp', 'uitests');

function findTests(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      return findTests(full);
    }

    return entry.name.endsWith('.test.js') ? [full] : [];
  });
}

function run() {
  fs.rmSync(outDir, { recursive: true, force: true });

  const compiled = spawnSync(
    process.execPath,
    [
      require.resolve('typescript/bin/tsc'),
      '-p',
      path.join(root, 'frontend', 'tsconfig.test.json'),
      '--noCheck'
    ],
    { stdio: 'inherit' }
  );

  if (compiled.status !== 0) {
    return compiled.status || 1;
  }

  const tests = findTests(outDir);

  if (!tests.length) {
    console.error('No UI tests found');
    return 1;
  }

  const tested = spawnSync(process.execPath, ['--test', ...tests], { stdio: 'inherit' });

  return tested.status === null ? 1 : tested.status;
}

process.exitCode = run();
