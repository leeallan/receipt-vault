// UK and US cuts share one edit. Each region has its own recordings (the US set has
// dollar demo data and receipts), so footage names and scene timings differ.
export type Region = "uk" | "us";

/** Footage file for a recording: store1.mp4 (UK) or store1_us.mp4 (US). */
export const footage = (name: string, region: Region) =>
  `footage/${name}${region === "us" ? "_us" : ""}.mp4`;
