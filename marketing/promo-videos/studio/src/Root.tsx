import React from "react";
import { Composition, Folder } from "remotion";
import { Store1, Store2, Store3, STORE1_SECONDS, STORE2_SECONDS, STORE3_SECONDS } from "./videos/Store";
import { S1, S2, S3, S4, S5, S6, S1_SECONDS, S2_SECONDS, S3_SECONDS, S4_SECONDS, S5_SECONDS, S6_SECONDS } from "./videos/Social";
import { Region } from "./lib/region";

const FPS = 30;
const f = (s: number) => Math.round(s * FPS);

const STORE = [
  { name: "Store1", component: Store1, seconds: STORE1_SECONDS },
  { name: "Store2", component: Store2, seconds: STORE2_SECONDS },
  { name: "Store3", component: Store3, seconds: STORE3_SECONDS },
];

// Sizes: App Store preview for 6.9" iPhone is 886×1920; social 9:16 is 1080×1920;
// Play Store / YouTube is 1920×1080. Same edit, layout adapts (lib/theme.ts useLayout).
const SIZES = [
  { suffix: "AppStore", width: 886, height: 1920 },
  { suffix: "Social", width: 1080, height: 1920 },
  { suffix: "Landscape", width: 1920, height: 1080 },
];

const SOCIAL: {
  name: string; component: React.FC<{ hook: string; region?: Region }>; seconds: number; hooks: string[]; region?: Region;
}[] = [
  { name: "S1-POV", component: S1, seconds: S1_SECONDS, hooks: [
    "POV: your boss wants *the receipt* from March",
    "Me pretending I *kept the receipt*",
    "The receipt is in my… *somewhere*",
  ] },
  { name: "S2-Fading", component: S2, seconds: S2_SECONDS, hooks: [
    "Your receipts are *disappearing* 👀",
    "Check your wallet. *Can you still read them?*",
    "That receipt you're keeping? *It's fading.*",
  ] },
  { name: "S3-Nobody", component: S3, seconds: S3_SECONDS, hooks: [
    "Things nobody tells you when you go *self-employed*",
    "HMRC can ask for receipts from *years ago*",
    "Your accountant *wishes you knew this*",
  ] },
  { name: "S4-Guess", component: S4, seconds: S4_SECONDS, hooks: [
    "Guess what *a year* of food shopping costs 👇",
    "I added up *every receipt* since January",
    "Drop your guess *before it stops* 👀",
  ] },
  { name: "S5-GroupChat", component: S5, seconds: S5_SECONDS, hooks: [
    "Every group holiday chat:",
    "When someone asks *who paid for what*",
    "Be the friend who *keeps the receipts*",
  ] },
  { name: "S6-TaxSeason", component: S6, seconds: S6_SECONDS, hooks: [
    "31 January is *closer than you think*",
    "Self-employed? *Do this before January*",
    "Sort your tax-return receipts *in minutes*",
  ] },
  // US versions of the two region-specific clips (IRS rules; Tax Day). The others travel.
  { name: "S3-Nobody-US", component: S3, seconds: S3_SECONDS, region: "us", hooks: [
    "Things nobody tells you when you go *self-employed*",
    "The IRS can ask for receipts from *years ago*",
    "Your accountant *wishes you knew this*",
  ] },
  { name: "S6-TaxSeason-US", component: S6, seconds: S6_SECONDS, region: "us", hooks: [
    "Tax Day is *April\u00A015.* Ready?",
    "Self-employed? *Do this before April*",
    "Sort your tax receipts *in minutes*",
  ] },
];

export const RemotionRoot: React.FC = () => {
  return (
    <>
      {(["uk", "us"] as const).map((region) => (
        <Folder key={region} name={region === "uk" ? "Store" : "Store-US"}>
          {STORE.flatMap((v) =>
            SIZES.map((s) => {
              const id = `${v.name}${region === "us" ? "-US" : ""}-${s.suffix}`;
              return (
                <Composition key={id} id={id} component={v.component} defaultProps={{ region }}
                  durationInFrames={f(v.seconds)} fps={FPS} width={s.width} height={s.height} />
              );
            }),
          )}
        </Folder>
      ))}
      <Folder name="Social">
        {SOCIAL.flatMap((v) =>
          v.hooks.map((hook, i) => (
            <Composition key={`${v.name}-${"ABC"[i]}`} id={`${v.name}-${"ABC"[i]}`} component={v.component}
              durationInFrames={f(v.seconds)} fps={FPS} width={1080} height={1920}
              defaultProps={{ hook, region: v.region ?? "uk" }} />
          )),
        )}
      </Folder>
    </>
  );
};
