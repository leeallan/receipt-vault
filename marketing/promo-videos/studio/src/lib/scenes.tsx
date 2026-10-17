import React from "react";
import { AbsoluteFill, Easing, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { Backdrop, Caption, markedWords, Phone, Sticker, Tap, Zoom } from "./components";
import { bodyFont, C, headFont, useLayout } from "./theme";

const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/** Caption over a stretch of screen recording — the workhorse scene. */
export const ClipScene: React.FC<{
  caption: string;
  kicker?: string;
  size?: number;
  src: string;
  trim: number;
  rate?: number;
  zooms?: Zoom[];
  taps?: Tap[];
  enter?: boolean;
  masks?: { x: number; y: number; w: number; h: number; color: string }[];
  sticker?: { text: string; at?: number; bg?: string };
}> = ({ caption, kicker, size, src, trim, rate, zooms, taps, enter, masks, sticker }) => {
  const L = useLayout();
  return (
    <AbsoluteFill>
      <Backdrop />
      <Phone src={src} trimBefore={trim} rate={rate} zooms={zooms} taps={taps} enter={enter} masks={masks} />
      <Caption text={caption} kicker={kicker} size={size} />
      {sticker ? (
        <Sticker
          text={sticker.text} at={sticker.at} bg={sticker.bg}
          x={L.landscape ? L.phone.x - 40 : L.w / 2} y={L.landscape ? L.h * 0.72 : L.phone.y + 60}
        />
      ) : null}
    </AbsoluteFill>
  );
};

/** Full-screen headline (hooks, beats like "Or…"). */
export const TitleScene: React.FC<{
  text: string; kicker?: string; size?: number; tone?: "purple" | "light" | "night"; color?: string; accent?: string;
}> = ({ text, kicker, size = 120, tone = "purple", color, accent }) => {
  const L = useLayout();
  return (
    <AbsoluteFill>
      <Backdrop tone={tone} />
      <Caption
        text={text} kicker={kicker} size={size} color={color ?? (tone === "light" ? C.ink : C.white)}
        accent={accent ?? (tone === "light" ? C.hiDark : C.hi)} align="center"
        box={{ x: 80, y: 0, w: L.w - 160, h: L.h }}
      />
    </AbsoluteFill>
  );
};

/** Hook text pinned at the top while a graphic plays underneath. */
const TopHook: React.FC<{ text: string; dark?: boolean }> = ({ text, dark }) => {
  const L = useLayout();
  return (
    <Caption
      text={text} size={L.landscape ? 70 : 78} color={dark ? C.ink : C.white}
      accent={dark ? C.hiDark : C.hi} align={L.landscape ? "left" : "center"}
      box={L.landscape ? { x: 120, y: 0, w: L.w * 0.42, h: L.h } : { x: 70, y: 110, w: L.w - 140, h: 380 }}
    />
  );
};

const graphicBox = (L: ReturnType<typeof useLayout>) =>
  L.landscape
    ? { x: L.w * 0.52, y: 90, w: L.w * 0.4, h: L.h - 180 }
    : { x: 70, y: 560, w: L.w - 140, h: L.h - 700 };

// ─── Notification pile-up ──────────────────────────────────────────────────

export type Note = { app: string; title: string; body: string; color: string };

export const NotificationPile: React.FC<{ hook: string; notes: Note[]; every?: number }> = ({ hook, notes, every = 0.42 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const b = graphicBox(L);
  const lastIn = (notes.length - 1) * every * fps + 8;
  const shake = frame > lastIn ? Math.sin(frame * 2.2) * interpolate(frame - lastIn, [0, 18], [10, 0], clamp) : 0;
  const u = L.u;
  return (
    <AbsoluteFill>
      <Backdrop tone="night" />
      <TopHook text={hook} />
      <div style={{ position: "absolute", left: b.x + shake, top: b.y, width: b.w }}>
        {notes.map((n, i) => {
          const s = spring({ frame: frame - (0.25 + i * every) * fps, fps, config: { damping: 15, mass: 0.7 } });
          return (
            <div
              key={i}
              style={{
                marginBottom: 22 * u, padding: `${26 * u}px ${30 * u}px`, borderRadius: 34 * u,
                background: "rgba(255,255,255,0.86)", backdropFilter: "blur(12px)",
                boxShadow: "0 18px 50px rgba(0,0,0,0.35)", display: "flex", gap: 24 * u, alignItems: "center",
                opacity: interpolate(s, [0, 0.3], [0, 1], clamp),
                translate: `0 ${interpolate(s, [0, 1], [-120, 0])}px`, scale: String(interpolate(s, [0, 1], [0.9, 1])),
              }}
            >
              <div style={{ width: 84 * u, height: 84 * u, borderRadius: 20 * u, background: n.color, flexShrink: 0,
                display: "flex", alignItems: "center", justifyContent: "center", color: C.white,
                fontFamily: headFont, fontWeight: 800, fontSize: 40 * u }}>
                {n.app[0]}
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: "flex", justifyContent: "space-between", fontFamily: bodyFont, fontSize: 26 * u, color: "#555" }}>
                  <span style={{ fontWeight: 600, textTransform: "uppercase", letterSpacing: 1 }}>{n.app}</span>
                  <span>now</span>
                </div>
                <div style={{ fontFamily: bodyFont, fontWeight: 700, fontSize: 36 * u, color: "#111", marginTop: 4 }}>{n.title}</div>
                <div style={{ fontFamily: bodyFont, fontSize: 32 * u, color: "#333", marginTop: 2 }}>{n.body}</div>
              </div>
            </div>
          );
        })}
      </div>
    </AbsoluteFill>
  );
};

// ─── Counter ───────────────────────────────────────────────────────────────

export const CounterScene: React.FC<{ to: number; label: string; source: string; dur?: number }> = ({ to, label, source, dur = 2.6 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const v = interpolate(frame, [6, dur * fps], [0, to], { ...clamp, easing: Easing.bezier(0.16, 1, 0.3, 1) });
  const done = frame >= dur * fps;
  const pop = spring({ frame: frame - dur * fps, fps, config: { damping: 9 } });
  const text = `£${to.toLocaleString("en-GB")}`;
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", flexDirection: "column" }}>
      <Backdrop tone="night" />
      <div style={{ fontFamily: headFont, fontWeight: 800, fontSize: Math.min(330, 1250 / text.length) * L.u, color: C.hi, letterSpacing: -8,
        scale: String(done ? 1 + 0.08 * Math.sin(pop * Math.PI) : 1), fontVariantNumeric: "tabular-nums" }}>
        £{Math.round(v).toLocaleString("en-GB")}
      </div>
      <div style={{ position: "relative", fontFamily: headFont, fontWeight: 700, fontSize: 60 * L.u, color: C.white, textAlign: "center",
        padding: "0 90px", opacity: interpolate(frame, [10, 25], [0, 1], clamp) }}>
        {label}
      </div>
      <div style={{ position: "absolute", bottom: 120 * L.u, fontFamily: bodyFont, fontSize: 30 * L.u, color: "rgba(255,255,255,0.7)",
        textAlign: "center", padding: "0 80px" }}>
        {source}
      </div>
    </AbsoluteFill>
  );
};

// ─── Numbered list ─────────────────────────────────────────────────────────

export const ListScene: React.FC<{ title: string; items: string[]; every: number; source?: string }> = ({ title, items, every, source }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const b = graphicBox(L);
  const u = L.u;
  return (
    <AbsoluteFill>
      <Backdrop tone="light" />
      <TopHook text={title} dark />
      <div style={{ position: "absolute", left: b.x, top: b.y + 20, width: b.w }}>
        {items.map((it, i) => {
          const s = spring({ frame: frame - (0.2 + i * every) * fps, fps, config: { damping: 14 } });
          const words = markedWords(it);
          return (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 30 * u, marginBottom: 40 * u,
              padding: `${34 * u}px ${36 * u}px`, borderRadius: 36 * u, background: C.white,
              boxShadow: "0 16px 40px rgba(59,47,175,0.14)",
              opacity: interpolate(s, [0, 0.3], [0, 1], clamp), translate: `${interpolate(s, [0, 1], [-200, 0])}px 0` }}>
              <div style={{ width: 96 * u, height: 96 * u, borderRadius: 999, background: C.primary, color: C.white, flexShrink: 0,
                display: "flex", alignItems: "center", justifyContent: "center", fontFamily: headFont, fontWeight: 800, fontSize: 52 * u }}>
                {i + 1}
              </div>
              <div style={{ fontFamily: headFont, fontWeight: 700, fontSize: 50 * u, color: C.ink, lineHeight: 1.15 }}>
                {words.map((w, j) => (
                  <span key={j} style={{ color: w.hi ? C.hiDark : C.ink }}>{w.word} </span>
                ))}
              </div>
            </div>
          );
        })}
      </div>
      {source ? (
        <div style={{ position: "absolute", left: 0, right: 0, bottom: 70 * u, textAlign: "center", fontFamily: bodyFont,
          fontSize: 28 * u, color: C.inkSoft, padding: "0 80px", opacity: interpolate(frame, [10, 25], [0, 1], clamp) }}>
          {source}
        </div>
      ) : null}
    </AbsoluteFill>
  );
};

// ─── Group chat ────────────────────────────────────────────────────────────

export type ChatMessage = { from: string; text: string; color?: string };

/** Generic group chat (deliberately not any real messenger's design). */
export const ChatScene: React.FC<{
  hook: string; group: string; emoji: string; members: number; messages: ChatMessage[]; every: number;
}> = ({ hook, group, emoji, members, messages, every }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const b = graphicBox(L);
  const u = L.u;
  return (
    <AbsoluteFill>
      <Backdrop />
      <TopHook text={hook} />
      <div style={{ position: "absolute", left: b.x, top: b.y, width: b.w, height: b.h - 40, borderRadius: 50 * u,
        background: "#F2F1F7", overflow: "hidden", boxShadow: "0 30px 80px rgba(10,6,40,0.45)" }}>
        <div style={{ padding: `${30 * u}px ${36 * u}px`, background: C.white, display: "flex", alignItems: "center", gap: 22 * u,
          borderBottom: "1px solid #E4E2EE" }}>
          <div style={{ width: 78 * u, height: 78 * u, borderRadius: 999, background: C.primary, display: "flex",
            alignItems: "center", justifyContent: "center", fontSize: 40 * u }}>{emoji}</div>
          <div>
            <div style={{ fontFamily: bodyFont, fontWeight: 700, fontSize: 38 * u, color: C.ink }}>{group}</div>
            <div style={{ fontFamily: bodyFont, fontSize: 28 * u, color: C.inkSoft }}>{members} members</div>
          </div>
        </div>
        <div style={{ padding: 34 * u }}>
          {messages.map((m, i) => {
            const s = spring({ frame: frame - (0.35 + i * every) * fps, fps, config: { damping: 13, mass: 0.6 } });
            const me = m.from === "You";
            return (
              <div key={i} style={{ display: "flex", justifyContent: me ? "flex-end" : "flex-start" }}>
                <div style={{ marginBottom: 24 * u, maxWidth: "84%", padding: `${20 * u}px ${30 * u}px`,
                  borderRadius: me ? `${36 * u}px ${12 * u}px ${36 * u}px ${36 * u}px` : `${12 * u}px ${36 * u}px ${36 * u}px ${36 * u}px`,
                  background: me ? C.primary : C.white, boxShadow: "0 4px 14px rgba(0,0,0,0.06)",
                  transformOrigin: me ? "right top" : "left top",
                  opacity: interpolate(s, [0, 0.3], [0, 1], clamp), scale: String(interpolate(s, [0, 1], [0.6, 1])) }}>
                  {me ? null : (
                    <div style={{ fontFamily: bodyFont, fontWeight: 700, fontSize: 28 * u, color: m.color ?? C.primary }}>{m.from}</div>
                  )}
                  <div style={{ fontFamily: bodyFont, fontSize: 40 * u, color: me ? C.white : C.ink, marginTop: 4 }}>{m.text}</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </AbsoluteFill>
  );
};

// ─── Fading thermal receipt ────────────────────────────────────────────────

/** A paper receipt whose print fades away, line by line, over `fadeFrom`…`fadeTo` seconds. */
export const FadingReceiptScene: React.FC<{
  hook: string; shop: string; lines: [string, string][]; total: string; fadeFrom?: number; fadeTo?: number;
}> = ({ hook, shop, lines, total, fadeFrom = 1.0, fadeTo = 3.6 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const L = useLayout();
  const b = graphicBox(L);
  const u = L.u;
  const t = frame / fps;
  const all = [shop, ...lines.map((l) => l[0]), total];
  // Each line fades on its own schedule, unevenly, the way real thermal print goes.
  const ink = (i: number) => {
    const jitter = ((i * 37) % 11) / 11;
    const start = fadeFrom + jitter * (fadeTo - fadeFrom) * 0.5;
    return interpolate(t, [start, start + (fadeTo - fadeFrom) * 0.6], [1, 0.04], clamp);
  };
  const s = spring({ frame, fps, config: { damping: 15 } });
  const mono = "'Courier New', Menlo, monospace";
  return (
    <AbsoluteFill>
      <Backdrop tone="night" />
      <TopHook text={hook} />
      <div style={{ position: "absolute", left: b.x + b.w * 0.08, top: b.y, width: b.w * 0.84,
        padding: `${50 * u}px ${46 * u}px ${70 * u}px`, background: "linear-gradient(180deg, #FFFDF6, #F4EFE2)",
        boxShadow: "0 30px 80px rgba(0,0,0,0.5)", rotate: "-2deg",
        opacity: interpolate(s, [0, 0.3], [0, 1], clamp), translate: `0 ${interpolate(s, [0, 1], [120, 0])}px`,
        clipPath: "polygon(0 0, 100% 0, 100% 97%, 95% 100%, 90% 97%, 85% 100%, 80% 97%, 75% 100%, 70% 97%, 65% 100%, 60% 97%, 55% 100%, 50% 97%, 45% 100%, 40% 97%, 35% 100%, 30% 97%, 25% 100%, 20% 97%, 15% 100%, 10% 97%, 5% 100%, 0 97%)" }}>
        <div style={{ fontFamily: mono, fontWeight: 700, fontSize: 64 * u, textAlign: "center", color: "#222", opacity: ink(0),
          marginBottom: 30 * u }}>{shop}</div>
        {lines.map(([d, p], i) => (
          <div key={i} style={{ display: "flex", justifyContent: "space-between", fontFamily: mono, fontSize: 36 * u,
            color: "#2A2A2A", opacity: ink(i + 1), marginBottom: 14 * u }}>
            <span>{d}</span><span>{p}</span>
          </div>
        ))}
        <div style={{ borderTop: "3px dashed #999", margin: `${20 * u}px 0`, opacity: ink(all.length) }} />
        <div style={{ display: "flex", justifyContent: "space-between", fontFamily: mono, fontWeight: 700, fontSize: 48 * u,
          color: "#222", opacity: ink(all.length - 1) }}>
          <span>TOTAL</span><span>{total}</span>
        </div>
      </div>
    </AbsoluteFill>
  );
};

// ─── Exported document ─────────────────────────────────────────────────────

/**
 * A page the app exported (rendered from the real file), on a paper card that pans
 * slowly down from `from` to `to` (fractions of the page's scrollable height).
 */
export const DocumentScene: React.FC<{
  caption: string; src: string; aspect: number; from?: number; to?: number; size?: number;
}> = ({ caption, src, aspect, from = 0, to = 0.15, size }) => {
  const frame = useCurrentFrame();
  const { fps, durationInFrames } = useVideoConfig();
  const L = useLayout();
  const b = graphicBox(L);
  const s = spring({ frame, fps, config: { damping: 15 } });
  const imgH = b.w * aspect;
  const pan = interpolate(frame, [0, durationInFrames], [from, to], { ...clamp, easing: Easing.inOut(Easing.quad) });
  return (
    <AbsoluteFill>
      <Backdrop />
      <Caption text={caption} size={size ?? (L.landscape ? 70 : 78)} align={L.landscape ? "left" : "center"}
        box={L.landscape ? { x: 120, y: 0, w: L.w * 0.42, h: L.h } : { x: 70, y: 110, w: L.w - 140, h: 380 }} />
      <div style={{ position: "absolute", left: b.x, top: b.y, width: b.w, height: b.h - 40, borderRadius: 30 * L.u,
        overflow: "hidden", background: C.white, boxShadow: "0 30px 80px rgba(10,6,40,0.45)",
        opacity: interpolate(s, [0, 0.3], [0, 1], clamp), translate: `0 ${interpolate(s, [0, 1], [120, 0])}px` }}>
        <Img src={staticFile(src)} style={{ position: "absolute", left: 0, width: b.w, height: imgH,
          top: -pan * Math.max(0, imgH - (b.h - 40)) }} />
      </div>
    </AbsoluteFill>
  );
};
