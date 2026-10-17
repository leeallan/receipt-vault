import React from "react";
import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { Video } from "@remotion/media";
import { bodyFont, C, headFont, useLayout } from "./theme";

const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/** Split "plain *highlighted phrase* plain" into words, flagging the highlighted ones. */
export const markedWords = (text: string) => {
  let on = false;
  return text.split(" ").map((w) => {
    const starts = w.startsWith("*");
    if (starts) on = true;
    const hi = on;
    if (w.endsWith("*") || (starts && w.length > 1 && w.slice(1).includes("*"))) on = false;
    return { word: w.replace(/\*/g, ""), hi };
  });
};

// ─── Background ────────────────────────────────────────────────────────────

export const Backdrop: React.FC<{ tone?: "purple" | "light" | "night" }> = ({ tone = "purple" }) => {
  const frame = useCurrentFrame();
  const drift = Math.sin(frame / 90) * 6;
  const bg =
    tone === "light"
      ? `radial-gradient(circle at ${30 + drift}% 20%, #FFFFFF 0%, ${C.bg} 50%, #C3B9FF 100%)`
      : tone === "night"
        ? `radial-gradient(circle at ${70 - drift}% 15%, #232338 0%, #131323 55%, ${C.night} 100%)`
        : `radial-gradient(circle at ${25 + drift}% 15%, ${C.primaryLight} 0%, ${C.primary} 45%, ${C.primaryDark} 100%)`;
  return <AbsoluteFill style={{ background: bg }} />;
};

// ─── Caption ───────────────────────────────────────────────────────────────

/**
 * Headline with word-by-word spring entrance. Wrap words in *asterisks* to colour them
 * with the accent. `size` is at 1080-wide portrait scale.
 */
export const Caption: React.FC<{
  text: string;
  kicker?: string;
  size?: number;
  color?: string;
  accent?: string;
  delay?: number;
  align?: "center" | "left";
  /** Override the layout's caption box (e.g. full-screen hook cards). */
  box?: { x: number; y: number; w: number; h: number };
}> = ({ text, kicker, size = 84, color = C.white, accent = C.hi, delay = 0, align, box }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const b = box ?? L.caption;
  const a = align ?? (L.landscape ? "left" : "center");
  const fs = size * L.u * (L.landscape ? 1.05 : 1);
  const words = markedWords(text);
  return (
    <div
      style={{
        position: "absolute", left: b.x, top: b.y, width: b.w, height: b.h,
        display: "flex", flexDirection: "column", justifyContent: "center",
        alignItems: a === "center" ? "center" : "flex-start", textAlign: a,
      }}
    >
      {kicker ? (
        <div
          style={{
            fontFamily: bodyFont, fontWeight: 600, fontSize: fs * 0.42, color,
            letterSpacing: 2, textTransform: "uppercase", marginBottom: fs * 0.18,
            translate: `0 ${interpolate(frame - delay, [0, 10], [20, 0], clamp)}px`,
            opacity: interpolate(frame - delay, [0, 10], [0, 0.85], clamp),
          }}
        >
          {kicker}
        </div>
      ) : null}
      <div
        style={{
          fontFamily: headFont, fontWeight: 800, fontSize: fs, lineHeight: 1.12, color,
          letterSpacing: -1, display: "flex", flexWrap: "wrap",
          justifyContent: a === "center" ? "center" : "flex-start", columnGap: fs * 0.26,
        }}
      >
        {words.map(({ word: clean, hi }, i) => {
          const s = spring({ frame: frame - delay - i * 2.5, fps, config: { damping: 14, mass: 0.6 } });
          return (
            <span
              key={i}
              style={{
                display: "inline-block", color: hi ? accent : color,
                opacity: interpolate(s, [0, 0.4], [0, 1], clamp),
                translate: `0 ${interpolate(s, [0, 1], [fs * 0.5, 0])}px`,
                scale: interpolate(s, [0, 1], [0.85, 1]),
              }}
            >
              {clean}
            </span>
          );
        })}
      </div>
    </div>
  );
};

// ─── Phone footage ─────────────────────────────────────────────────────────

export type Zoom = { at: number; to?: number; scale: number; x: number; y: number };
export type Tap = { at: number; x: number; y: number };

/**
 * Screen recording in a rounded frame. Times are in seconds *of this scene*.
 * Zoom focus x/y and tap x/y are fractions (0–1) of the footage.
 */
export const Phone: React.FC<{
  src: string;
  trimBefore?: number;
  rate?: number;
  zooms?: Zoom[];
  taps?: Tap[];
  enter?: boolean;
  bezel?: boolean;
  /** Patches painted over the footage (fractions of the footage), e.g. a storefront-specific price. */
  masks?: { x: number; y: number; w: number; h: number; color: string }[];
}> = ({ src, trimBefore = 0, rate = 1, zooms = [], taps = [], enter = false, bezel = true, masks = [] }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const p = L.phone;
  const t = frame / fps;

  // Zoom: ease in to each zoom and back out at `to` (if given).
  let scale = 1, fx = 0.5, fy = 0.5;
  for (const z of zooms) {
    const inP = interpolate(t, [z.at, z.at + 0.45], [0, 1], { ...clamp, easing: Easing.bezier(0.33, 1, 0.68, 1) });
    const outP = z.to === undefined ? 0 : interpolate(t, [z.to, z.to + 0.45], [0, 1], { ...clamp, easing: Easing.bezier(0.33, 1, 0.68, 1) });
    const k = inP * (1 - outP);
    if (k > 0) {
      scale = 1 + (z.scale - 1) * k;
      fx = 0.5 + (z.x - 0.5) * k;
      fy = 0.5 + (z.y - 0.5) * k;
    }
  }

  const s = enter ? spring({ frame, fps, config: { damping: 16, mass: 0.8 } }) : 1;
  const radius = p.w * 0.12;
  const border = bezel ? p.w * 0.028 : 0;

  return (
    <div
      style={{
        position: "absolute", left: p.x, top: p.y, width: p.w, height: p.h,
        translate: `0 ${interpolate(s, [0, 1], [p.h * 0.35, 0])}px`,
        opacity: interpolate(s, [0, 0.3], [0, 1], clamp),
      }}
    >
      <div
        style={{
          position: "absolute", inset: -border, borderRadius: radius + border,
          background: bezel ? "#0E0C1F" : "transparent",
          boxShadow: "0 40px 90px rgba(10,6,40,0.45)",
        }}
      />
      <div style={{ position: "absolute", inset: 0, borderRadius: radius, overflow: "hidden", background: C.screen }}>
        <div
          style={{
            position: "absolute", inset: 0, scale: String(scale),
            transformOrigin: `${fx * 100}% ${fy * 100}%`,
          }}
        >
          <Video
            src={staticFile(src)}
            trimBefore={Math.round(trimBefore * fps)}
            playbackRate={rate}
            muted
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
          />
          {masks.map((m, i) => (
            <div
              key={`m${i}`}
              style={{
                position: "absolute", left: `${m.x * 100}%`, top: `${m.y * 100}%`,
                width: `${m.w * 100}%`, height: `${m.h * 100}%`, background: m.color,
              }}
            />
          ))}
          {taps.map((tp, i) => (
            <TapRing key={i} at={tp.at} x={tp.x} y={tp.y} size={p.w * 0.16} />
          ))}
        </div>
      </div>
    </div>
  );
};

const TapRing: React.FC<{ at: number; x: number; y: number; size: number }> = ({ at, x, y, size }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const f = frame - at * fps;
  if (f < -4 || f > 16) return null;
  const k = interpolate(f, [-4, 0, 16], [0, 0.6, 1], clamp);
  return (
    <div
      style={{
        position: "absolute", left: `${x * 100}%`, top: `${y * 100}%`, width: size, height: size,
        marginLeft: -size / 2, marginTop: -size / 2, borderRadius: "50%",
        background: "rgba(255,255,255,0.55)", border: `4px solid ${C.accent}`,
        scale: String(interpolate(k, [0, 0.6, 1], [0.4, 0.8, 1.5])),
        opacity: interpolate(k, [0, 0.6, 1], [0, 1, 0]),
      }}
    />
  );
};

// ─── Pill / sticker ────────────────────────────────────────────────────────

export const Sticker: React.FC<{
  text: string; x: number; y: number; at?: number; bg?: string; color?: string; size?: number; rotate?: number;
}> = ({ text, x, y, at = 0, bg = C.accent, color = C.white, size = 44, rotate = -4 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const s = spring({ frame: frame - at * fps, fps, config: { damping: 11, mass: 0.6 } });
  return (
    <div
      style={{
        position: "absolute", left: x, top: y, translate: "-50% -50%",
        padding: `${size * 0.35 * L.u}px ${size * 0.6 * L.u}px`, borderRadius: 999, background: bg, color,
        fontFamily: headFont, fontWeight: 700, fontSize: size * L.u, whiteSpace: "nowrap",
        boxShadow: "0 18px 40px rgba(10,6,40,0.35)", rotate: `${rotate}deg`,
        scale: String(s), opacity: interpolate(s, [0, 0.3], [0, 1], clamp),
      }}
    >
      {text}
    </div>
  );
};

// ─── End card ──────────────────────────────────────────────────────────────

export const EndCard: React.FC<{ line?: string; sub?: string }> = ({
  line = "Never lose a receipt again.",
  sub = "Free on the App Store",
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const s = spring({ frame, fps, config: { damping: 13 } });
  const s2 = spring({ frame: frame - 8, fps, config: { damping: 15 } });
  const s3 = spring({ frame: frame - 16, fps, config: { damping: 15 } });
  const icon = (L.landscape ? 260 : 300) * L.u;
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", flexDirection: "column" }}>
      <Backdrop />
      <div
        style={{
          width: icon, height: icon, borderRadius: icon * 0.23, overflow: "hidden",
          boxShadow: "0 30px 80px rgba(10,6,40,0.5), inset 0 0 0 3px rgba(255,255,255,0.18)",
          display: "flex", alignItems: "center", justifyContent: "center",
          scale: String(s), rotate: `${interpolate(s, [0, 1], [-12, 0])}deg`,
        }}
      >
        <Img src={staticFile("icon.png")} style={{ width: "100%", height: "100%" }} />
      </div>
      <div
        style={{
          position: "relative", fontFamily: headFont, fontWeight: 800, color: C.white, fontSize: 118 * L.u, marginTop: 40 * L.u,
          letterSpacing: -3, opacity: s2, translate: `0 ${interpolate(s2, [0, 1], [40, 0])}px`,
        }}
      >
        Receipt Vault
      </div>
      <div
        style={{
          fontFamily: headFont, fontWeight: 600, color: C.white, fontSize: 54 * L.u, opacity: s3 * 0.95,
          textAlign: "center", padding: "0 60px", translate: `0 ${interpolate(s3, [0, 1], [30, 0])}px`,
        }}
      >
        {line}
      </div>
      <div
        style={{
          marginTop: 60 * L.u, padding: `${22 * L.u}px ${48 * L.u}px`, borderRadius: 999, background: C.white,
          color: C.primary, fontFamily: headFont, fontWeight: 700, fontSize: 44 * L.u,
          opacity: s3, scale: String(interpolate(s3, [0, 1], [0.8, 1])),
        }}
      >
        {sub}
      </div>
    </AbsoluteFill>
  );
};

// ─── Simple fade wrapper ───────────────────────────────────────────────────

export const FadeIn: React.FC<{ children: React.ReactNode; dur?: number }> = ({ children, dur = 6 }) => {
  const frame = useCurrentFrame();
  return <AbsoluteFill style={{ opacity: interpolate(frame, [0, dur], [0, 1], clamp) }}>{children}</AbsoluteFill>;
};
