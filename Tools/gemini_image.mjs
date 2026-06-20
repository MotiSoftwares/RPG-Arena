// Minimal, robust Gemini image generator for the project.
// Why this exists: the nano-banana-mcp npm package (v1.0.3, unmaintained) is pinned
// to the retired model "gemini-2.5-flash-image-preview" and 404s. This script calls
// the current free GA model "gemini-2.5-flash-image" directly, so it can't rot.
//
// Usage:  node Tools/gemini_image.mjs "<prompt>" "<output_path.png>" [model]
// The API key is NEVER hardcoded or committed: it is read from the GEMINI_API_KEY env
// var if present, otherwise pulled from the local Claude MCP config (~/.claude.json),
// where it already lives for the nano-banana MCP server. So the key stays in one place.

import fs from "fs";
import os from "os";

// Resolve the Gemini key without ever writing it into the repo.
function resolveKey() {
  if (process.env.GEMINI_API_KEY) return process.env.GEMINI_API_KEY;
  // Fall back to the project-local MCP config that already holds the key.
  const cfgPath = os.homedir() + "/.claude.json";
  const cfg = JSON.parse(fs.readFileSync(cfgPath, "utf8"));
  for (const k of Object.keys(cfg.projects || {})) {
    const env = cfg.projects[k].mcpServers?.["nano-banana"]?.env;
    if (env?.GEMINI_API_KEY) return env.GEMINI_API_KEY;
  }
  throw new Error("GEMINI_API_KEY not found in env or ~/.claude.json");
}

const prompt = process.argv[2];
const outPath = process.argv[3];
// Default to the confirmed free-tier image model; allow override as the 3rd arg.
const model = process.argv[4] || "gemini-2.5-flash-image";
if (!prompt || !outPath) {
  console.error('Usage: node Tools/gemini_image.mjs "<prompt>" "<output.png>" [model]');
  process.exit(2);
}

const key = resolveKey();
const url = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${key}`;

// Single request: text prompt in, inline image out.
const res = await fetch(url, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ contents: [{ parts: [{ text: prompt }] }] }),
});

const json = await res.json();
// The image comes back as inline base64 data on one of the response parts.
const parts = json?.candidates?.[0]?.content?.parts || [];
const inline = parts.find((p) => p.inlineData)?.inlineData?.data;
if (!inline) {
  console.error("No image returned. Response head:", JSON.stringify(json).slice(0, 600));
  process.exit(1);
}

fs.writeFileSync(outPath, Buffer.from(inline, "base64"));
console.log(`Saved ${outPath} (${fs.statSync(outPath).size} bytes) using ${model}`);
