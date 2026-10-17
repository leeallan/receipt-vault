import React from "react";
import { Series, useVideoConfig } from "remotion";
import { EndCard, FadeIn } from "../lib/components";
import { ClipScene } from "../lib/scenes";

// Store videos double as App Store previews, so every scene is real app footage with a
// caption — no prices (they vary by storefront) and no people or hands.
// Footage times (trim) are seconds into the recording; see capture/flows/*.yaml.
// All shops and amounts are fictional demo data.

/** Store 1 — snap a receipt: it reads the shop, total and date; file it; done. */
export const STORE1_SECONDS = 17.7;
export const Store1: React.FC = () => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={2.6 * fps}>
        <ClipScene enter caption="Never type out a receipt *again*" src="footage/store1.mp4" trim={21.5} rate={0.45}
          zooms={[{ at: 0.2, scale: 1.12, x: 0.5, y: 0.45 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.4 * fps}>
        <ClipScene caption="Snap it, or *pick a photo*" src="footage/store1.mp4" trim={9.0} rate={2.0} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.2 * fps}>
        <ClipScene caption="Shop, total and date — *read for you*" size={76} src="footage/store1.mp4" trim={19.6}
          zooms={[{ at: 2.3, scale: 1.12, x: 0.5, y: 0.45 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="Business or personal? *One tap.*" src="footage/store1.mp4" trim={33.4}
          zooms={[{ at: 0, scale: 1.12, x: 0.5, y: 0.72 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.2 * fps}>
        <ClipScene caption="Saved. *Totals update instantly.*" src="footage/store1.mp4" trim={41.6} rate={1.2}
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
export const Store2: React.FC = () => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene enter caption="A year of receipts. *Zero shoeboxes.*" size={78} src="footage/store2.mp4" trim={15.0} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene caption="Every month, *added up*" src="footage/store2.mp4" trim={8.3} rate={1.2}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.2 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.2 * fps}>
        <ClipScene caption="Budgets that *keep you honest*" src="footage/store2.mp4" trim={12.9}
          zooms={[{ at: 0, scale: 1.08, x: 0.5, y: 0.3 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={1.9 * fps}>
        <ClipScene caption="Business only? *One tap.*" src="footage/store2.mp4" trim={34.6} rate={0.7} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene caption="Find any receipt *in seconds*" src="footage/store2.mp4" trim={37.6} rate={1.8} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.4 * fps}>
        <ClipScene caption="The *original photo*, kept safe" src="footage/store2.mp4" trim={43.8}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.25 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.5 * fps}>
        <FadeIn><EndCard /></FadeIn>
      </Series.Sequence>
    </Series>
  );
};

/** Store 3 — Premium: every item read off the receipt, itemised spreadsheets, tax report. */
export const STORE3_SECONDS = 16.9;
export const Store3: React.FC = () => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene enter caption="Every item. *Not just the total.*" src="footage/store3.mp4" trim={20.6}
          zooms={[{ at: 1.3, scale: 1.12, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="Prices, quantities *and savings*" src="footage/store3.mp4" trim={23.5} rate={2.2} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.6 * fps}>
        <ClipScene caption="Export a whole year *in one tap*" src="footage/store3.mp4" trim={35.8} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.2 * fps}>
        <ClipScene caption="*Itemised* spreadsheets" src="footage/store3.mp4" trim={41.55} rate={0.35}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="A tax report, *ready for your accountant*" size={74} src="footage/store3.mp4" trim={44.8}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.62 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.5 * fps}>
        <FadeIn><EndCard line="Unlock Premium once. Keep it forever." /></FadeIn>
      </Series.Sequence>
    </Series>
  );
};
