// Free, keyless image generator for the project (Pollinations.ai -> flux).
// Why this is the default 2D path: the Gemini free image tier returned limit:0 on our
// key (paid-only), and the nano-banana MCP is pinned to a retired model. Pollinations
// needs no account/key/quota and uses the open-licensed `flux` model, so it is a clean
// $0 autonomous source for ability icons / portraits / UI sprites in a graded project.
// (Disclose "images generated with Pollinations.ai (flux)" in the README credits.)
//
// Usage: node Tools/pollinations_image.mjs "<prompt>" "<output.png>" [width] [height] [seed]
// For higher-quality art later, swap to Tools/gemini_image.mjs (needs Gemini billing) or a paid provider.

import fs from "fs";

const prompt = process.argv[2];
const outPath = process.argv[3];
const width = process.argv[4] || "512";
const height = process.argv[5] || "512";
// A fixed seed makes a set of icons/portraits style-consistent and reproducible.
const seed = process.argv[6] || "1";
if (!prompt || !outPath) {
  console.error('Usage: node Tools/pollinations_image.mjs "<prompt>" "<output.png>" [w] [h] [seed]');
  process.exit(2);
}

// flux = open license; nologo strips any watermark; enhance improves prompt adherence.
const url =
  `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}` +
  `?model=flux&width=${width}&height=${height}&seed=${seed}&nologo=true`;

const res = await fetch(url, { redirect: "follow" });
if (!res.ok) {
  console.error(`Pollinations error ${res.status}: ${(await res.text()).slice(0, 300)}`);
  process.exit(1);
}
const buf = Buffer.from(await res.arrayBuffer());
fs.writeFileSync(outPath, buf);
console.log(`Saved ${outPath} (${buf.length} bytes, ${width}x${height})`);
