import React from "react";
import { Series, useVideoConfig } from "remotion";
import { EndCard, FadeIn } from "../lib/components";
import {
  ChatScene, ClipScene, CounterScene, DocumentScene, FadingReceiptScene, ListScene, NotificationPile, TitleScene,
} from "../lib/scenes";

// Short native-feeling cuts for TikTok / Reels / Shorts. Silent by design: post with a
// trending sound added in-app. Each takes `hook` so the A/B/C variants share one edit.
// All shops, people and amounts are fictional demo data.

type HookProps = { hook: string };

// public/tax-report.png is page 1 of the tax & expense report the app exported during the
// social_tax recording (qlmanage -t -s 2400 on the PDF from the app's Caches/exports).
const REPORT_ASPECT = 2400 / 1695;

const End: React.FC<{ line?: string; sub?: string }> = (p) => <FadeIn><EndCard {...p} /></FadeIn>;

/** S1 — POV: someone at work wants a receipt you no longer have. */
export const S1_SECONDS = 12.0;
export const S1: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={3.3 * fps}>
        <NotificationPile hook={hook} notes={[
          { app: "Finance", title: "Expense claims", body: "Close Friday — receipts please 🧾", color: "#6C63FF" },
          { app: "Sam (Manager)", title: "Client lunch", body: "Can you send me that receipt?", color: "#FF9F43" },
          { app: "Finance", title: "Missing receipt", body: "Train ticket, 13 Sep — £34.76", color: "#6C63FF" },
          { app: "Accounts", title: "Reminder", body: "No receipt, no refund 🙃", color: "#01CBC6" },
        ]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={1 * fps}>
        <TitleScene text="Or…" size={180} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3 * fps}>
        <ClipScene enter caption="Search. *Found it.*" src="footage/store2.mp4" trim={37.6} rate={1.8} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.2 * fps}>
        <ClipScene caption="Photo and all 📸" src="footage/store2.mp4" trim={43.8}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.25 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <End />
      </Series.Sequence>
    </Series>
  );
};

/** S2 — thermal receipts fade; a photo doesn't. */
export const S2_SECONDS = 11.4;
export const S2: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={4.4 * fps}>
        <FadingReceiptScene hook={hook} shop="BRICKWELL" total="£47.68" lines={[
          ["TIMBER 47X100", "14.40"], ["SCREWS BOX 200", "8.95"], ["WOOD GLUE 1L", "6.49"],
          ["MASKING TAPE", "5.25"], ["DUST SHEET", "7.99"], ["SANDPAPER", "4.60"],
        ]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={1.3 * fps}>
        <TitleScene text="Unless you *snap it.*" size={130} tone="night" />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.2 * fps}>
        <ClipScene enter caption="Scan it once. *Keep it forever.*" src="footage/store1.mp4" trim={19.6}
          zooms={[{ at: 2.3, scale: 1.12, x: 0.5, y: 0.45 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <End />
      </Series.Sequence>
    </Series>
  );
};

/** S3 — things nobody tells you about keeping records when self-employed (GOV.UK). */
export const S3_SECONDS = 14.4;
export const S3: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={8.6 * fps}>
        <ListScene title={hook} every={1.8} source="Source: GOV.UK — “Business records if you’re self-employed”" items={[
          "Keep records *5 years* after the 31 Jan deadline",
          "Paper or digital — *both count*",
          "Unreadable records can mean *a penalty*",
          "Split business from *personal*",
        ]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.3 * fps}>
        <ClipScene enter caption="Receipt Vault keeps *all of it*" src="footage/store2.mp4" trim={15.0} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <End />
      </Series.Sequence>
    </Series>
  );
};

/** S4 — guess the number (the demo account's grocery total from its tax report). */
export const S4_SECONDS = 12.4;
export const S4: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={2.3 * fps}>
        <TitleScene text={hook} size={104} tone="night" />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.6 * fps}>
        <CounterScene to={2809} label="on groceries since January" source="Demo account in Receipt Vault" />
      </Series.Sequence>
      <Series.Sequence durationInFrames={4 * fps}>
        <DocumentScene caption="Every category, *added up for you*" src="tax-report.png" aspect={REPORT_ASPECT}
          from={0.05} to={0.3} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <End />
      </Series.Sequence>
    </Series>
  );
};

/** S5 — the holiday group chat. */
export const S5_SECONDS = 12.6;
export const S5: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={5 * fps}>
        <ChatScene hook={hook} group="Lisbon trip ✈️" emoji="🏖️" members={6} every={0.95} messages={[
          { from: "Jess", text: "ok who paid for what 😅", color: "#FF6CAE" },
          { from: "Tom", text: "I got the apartment… I think?", color: "#FF9F43" },
          { from: "Priya", text: "send receipts pls 🙏", color: "#01CBC6" },
          { from: "You", text: "already sorted 👇" },
        ]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.1 * fps}>
        <ClipScene enter caption="Every receipt, *one search away*" src="footage/store2.mp4" trim={37.6} rate={1.8} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2 * fps}>
        <ClipScene caption="Photo and all 📸" src="footage/store2.mp4" trim={43.8}
          zooms={[{ at: 0, scale: 1.1, x: 0.5, y: 0.25 }]} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <End />
      </Series.Sequence>
    </Series>
  );
};

/** S6 — Self Assessment season (post late December to mid January). */
export const S6_SECONDS = 14.2;
export const S6: React.FC<HookProps> = ({ hook }) => {
  const { fps } = useVideoConfig();
  return (
    <Series>
      <Series.Sequence durationInFrames={2.5 * fps}>
        <TitleScene text={hook} size={100} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene enter caption="Pick your dates. *Tap export.*" src="footage/store3.mp4" trim={35.8} rate={1.5} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={3.4 * fps}>
        <DocumentScene caption="Business spend, VAT, categories — *done*" size={74} src="tax-report.png"
          aspect={REPORT_ASPECT} from={0} to={0.2} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.8 * fps}>
        <ClipScene caption="Every business expense, *listed*" src="footage/social_tax.mp4" trim={25.0} rate={1.3} />
      </Series.Sequence>
      <Series.Sequence durationInFrames={2.7 * fps}>
        <End line="Tax & expense reports with Premium." />
      </Series.Sequence>
    </Series>
  );
};
