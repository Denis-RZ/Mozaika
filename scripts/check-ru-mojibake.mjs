#!/usr/bin/env node

import fs from "node:fs/promises";
import path from "node:path";

const projectRoot = process.cwd();
const includeTargets = [
  "frontend/src",
  "backend-dotnet",
  "docs",
  "README.md",
  "start-local.ps1",
  "stop-local.ps1",
];

const skipDirs = new Set([
  ".git",
  "node_modules",
  "dist",
  "bin",
  "obj",
  "archive",
  "data",
  ".idea",
  ".vscode",
]);

const textExtensions = new Set([
  ".ts",
  ".tsx",
  ".js",
  ".jsx",
  ".mjs",
  ".cjs",
  ".json",
  ".css",
  ".html",
  ".md",
  ".cs",
  ".csproj",
  ".props",
  ".targets",
  ".ps1",
  ".yml",
  ".yaml",
  ".toml",
  ".xml",
  ".txt",
  ".env",
  ".example",
]);

// Typical mojibake markers when UTF-8 Cyrillic is decoded as latin/cp1252.
const mojibakeRegex =
  /(?:\u00D0[\u0080-\u00BF]|\u00D1[\u0080-\u00BF]|\u00C3[\u0080-\u00BF]|\u00C2[\u0080-\u00BF]|\u00E2[\u0080-\u00BF]{2}|\u00EF\u00BF\u00BD|\uFFFD)/u;

async function pathExists(target) {
  try {
    await fs.access(target);
    return true;
  } catch {
    return false;
  }
}

function shouldScanFile(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  if (textExtensions.has(ext)) {
    return true;
  }

  const baseName = path.basename(filePath).toLowerCase();
  return baseName === "readme" || baseName.startsWith(".env");
}

async function* walk(targetPath) {
  const stat = await fs.stat(targetPath);
  if (stat.isFile()) {
    yield targetPath;
    return;
  }

  if (!stat.isDirectory()) {
    return;
  }

  const entries = await fs.readdir(targetPath, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.name.startsWith(".") && entry.name !== ".env.example") {
      continue;
    }
    if (skipDirs.has(entry.name)) {
      continue;
    }

    const fullPath = path.join(targetPath, entry.name);
    if (entry.isDirectory()) {
      yield* walk(fullPath);
      continue;
    }

    if (entry.isFile()) {
      yield fullPath;
    }
  }
}

async function scanFile(filePath) {
  if (!shouldScanFile(filePath)) {
    return [];
  }

  let content;
  try {
    content = await fs.readFile(filePath, "utf8");
  } catch {
    return [];
  }

  if (!mojibakeRegex.test(content)) {
    return [];
  }

  const lines = content.split(/\r?\n/);
  const findings = [];
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    if (mojibakeRegex.test(line)) {
      findings.push({
        line: i + 1,
        snippet: line.trim().slice(0, 220),
      });
    }
  }

  return findings;
}

async function main() {
  const allFindings = [];

  for (const target of includeTargets) {
    const absolute = path.join(projectRoot, target);
    if (!(await pathExists(absolute))) {
      continue;
    }

    for await (const filePath of walk(absolute)) {
      const findings = await scanFile(filePath);
      if (findings.length === 0) {
        continue;
      }

      allFindings.push({
        filePath: path.relative(projectRoot, filePath).replaceAll("\\", "/"),
        findings,
      });
    }
  }

  if (allFindings.length === 0) {
    console.log("OK: no Russian mojibake markers detected.");
    return;
  }

  console.error("Detected possible Russian mojibake markers:");
  for (const fileResult of allFindings) {
    console.error(`- ${fileResult.filePath}`);
    for (const finding of fileResult.findings.slice(0, 6)) {
      console.error(`  ${finding.line}: ${finding.snippet}`);
    }
    if (fileResult.findings.length > 6) {
      console.error(`  ... and ${fileResult.findings.length - 6} more lines`);
    }
  }

  process.exitCode = 1;
}

main().catch((error) => {
  console.error("Failed to run encoding check:", error);
  process.exitCode = 2;
});
