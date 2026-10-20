import React from "react";
import { Series, useVideoConfig } from "remotion";
import { EndCard, FadeIn } from "../lib/components";
import { ClipScene } from "../lib/scenes";
import { footage, Region } from "../lib/region";

type Props = { region?: Region };

// Store videos double as App Store previews, so every scene is real app footage with a
// caption — no prices (they vary by storefront) and no people or hands.
// Footage times (trim) are seconds into each region's recording; see capture/flows/*.yaml.
// All shops and amounts are fictional demo data.

/** Store 1 — snap a receipt: it reads the shop, total and date; file it; done. */
export const STORE1_SECONDS = 17.7;
const T1 = {
  uk: { hook: 21.5, pick: 9.0, read: 19.6, business: 33.4, saved: 41.6 },
  us: { hook: 22.2, pick: 9.2, read: 19.6, business: 34.6, saved: 42.6 },
};
export const Store1: React.FC<Props> = ({ region = "uk" }) => {
  const { fps } = useVideoConfig();
  const src = footage("store1", region);
  const t = T1[region];
  return (
    <Series>
      <Series.Sequence durationInFrames={2.6 * fps}>
        <ClipScene enter caption="Never type out a receipt *again*" src={src} trim={t.hook} rate={0.45}
          zooms={[{ at: 0.2, scale: 1.12, x: 0.5, y: 0.45 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.4 * fps}>
        <ClipScene caption="Snap it, or *pick a photo*" src={src} trim={t.pick} rate={2.0} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.2 * fps}>
        <ClipScene caption="Shop, total and date — *read for you*" size={76} src={src} trim={t.read}
          zooms={[{ at: 2.3, scale: 1.12, x: 0.5, y: 0.45 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="Business or personal? *One tap.*" src={src} trim={t.business}
          zooms={[{ at: 0, scale: 1.12, x: 0.5, y: 0.72 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.2 * fps}>
        <ClipScene caption="Saved. *Totals update instantly.*" src={src} trim={t.saved} rate={1.2}
          zooms={[{ at: 2.0, scale: 1.1, x: 0.5, y: 0.35 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.5 * fps}>
        <FadeIn><EndCard /></FadeIn>
      </Series.Sequence>
    </Series>
  );
};

/** Store 2 — a year of receipts: totals by month, budgets, filters, search, the photo. */
export const STORE2_SECONDS = 18.8;
const T2 = {
  uk: { list: 15.0, months: 8.3, budget: 12.9, business: 34.6, search: 37.6, photo: 43.8 },
  us: { list: 16.0, months: 8.3, budget: 12.9, business: 34.6, search: 37.6, photo: 43.0 },
};
export const Store2: React.FC<Props> = ({ region = "uk" }) => {
  const { fps } = useVideoConfig();
  const src = footage("store2", region);
  const t = T2[region];
  return (
    <Series>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene enter caption="A year of receipts. *Zero shoeboxes.*" size={78} src={src} trim={t.list} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene caption="Every month, *added up*" src={src} trim={t.months} rate={1.2}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.2 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.2 * fps}>
        <ClipScene caption="Budgets that *keep you honest*" src={src} trim={t.budget}
          zooms={[{ at: 0, scale: 1.08, x: 0.5, y: 0.3 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={1.9 * fps}>
        <ClipScene caption="Business only? *One tap.*" src={src} trim={t.business} rate={0.7} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene caption="Find any receipt *in seconds*" src={src} trim={t.search} rate={1.8} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.4 * fps}>
        <ClipScene caption="The *original photo*, kept safe" src={src} trim={t.photo}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.25 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.5 * fps}>
        <FadeIn><EndCard /></FadeIn>
      </Series.Sequence>
    </Series>
  );
};

/** Store 3 — Premium: every item read off the receipt, itemised spreadsheets, tax report.
 *  The app's own screens still say "Itemised" (it isn't localised); only captions change. */
export const STORE3_SECONDS = 16.9;
const T3 = {
  uk: { items: 20.6, scroll: 23.5, export: 35.8, sheet: 41.55, report: 44.8 },
  us: { items: 20.6, scroll: 23.3, export: 35.5, sheet: 40.45, report: 43.1 },
};
export const Store3: React.FC<Props> = ({ region = "uk" }) => {
  const { fps } = useVideoConfig();
  const src = footage("store3", region);
  const t = T3[region];
  return (
    <Series>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene enter caption="Every item. *Not just the total.*" src={src} trim={t.items}
          zooms={[{ at: 1.3, scale: 1.12, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="Prices, quantities *and savings*" src={src} trim={t.scroll} rate={2.2} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.6 * fps}>
        <ClipScene caption="Export a whole year *in one tap*" src={src} trim={t.export} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.2 * fps}>
        <ClipScene caption={region === "us" ? "*Itemized* spreadsheets" : "*Itemised* spreadsheets"} src={src} trim={t.sheet} rate={region === "us" ? 0.17 : 0.35}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="A tax report, *ready for your accountant*" size={74} src={src} trim={t.report}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.5 * fps}>
        <FadeIn><EndCard line="Unlock Premium once. Keep it forever." /></FadeIn>
      </Series.Sequence>
    </Series>
  );
};
