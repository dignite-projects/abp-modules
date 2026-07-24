const baseUrl = process.env.FILE_EXPLORER_BASE_URL ?? 'https://localhost:44390';
const containerName = process.env.FILE_EXPLORER_CONTAINER ?? 'Default';
const configurationUrl = new URL(
  `/api/file-explorer/files/${encodeURIComponent(containerName)}/configuration`,
  baseUrl
);

const response = await fetch(configurationUrl, {
  headers: { Accept: 'application/json' }
});

if (!response.ok) {
  throw new Error(`File Explorer configuration request failed: ${response.status} ${response.statusText}`);
}

const configuration = await response.json();
if (typeof configuration.maxBlobSize !== 'number') {
  throw new Error('File Explorer configuration response did not contain maxBlobSize.');
}

console.log(`File Explorer HTTP smoke test passed for ${configurationUrl}.`);
