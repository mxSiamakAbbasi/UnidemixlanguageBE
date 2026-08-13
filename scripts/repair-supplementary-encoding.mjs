import fs from "node:fs";

const path = new URL("../src/Unidemix.Api/Content/Learning/German/Supplementary/supplementary-v1.json", import.meta.url);
const packageData = JSON.parse(fs.readFileSync(path, "utf8").replace(/^\uFEFF/u, ""));
const windows1252 = new Map([[0x20ac,0x80],[0x201a,0x82],[0x192,0x83],[0x201e,0x84],[0x2026,0x85],[0x2020,0x86],[0x2021,0x87],[0x2c6,0x88],[0x2030,0x89],[0x160,0x8a],[0x2039,0x8b],[0x152,0x8c],[0x17d,0x8e],[0x2018,0x91],[0x2019,0x92],[0x201c,0x93],[0x201d,0x94],[0x2022,0x95],[0x2013,0x96],[0x2014,0x97],[0x2dc,0x98],[0x2122,0x99],[0x161,0x9a],[0x203a,0x9b],[0x153,0x9c],[0x17e,0x9e],[0x178,0x9f]]);
function decode(value) {
  if (!/[ØÙÛâ]/u.test(value)) return value;
  const bytes = [];
  for (const char of value) {
    const code = char.codePointAt(0);
    const byte = windows1252.get(code) ?? code;
    if (byte > 255) return value;
    bytes.push(byte);
  }
  return Buffer.from(bytes).toString("utf8");
}
function repair(value) {
  if (typeof value === "string") return decode(value);
  if (Array.isArray(value)) return value.map(repair);
  if (value && typeof value === "object") for (const key of Object.keys(value)) value[key] = repair(value[key]);
  return value;
}
repair(packageData);
packageData.version = "1.1.2";
fs.writeFileSync(path, `${JSON.stringify(packageData, null, 2)}\n`, "utf8");
