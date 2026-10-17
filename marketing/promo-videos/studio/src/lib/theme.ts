import { loadFont as loadPoppins } from "@remotion/google-fonts/Poppins";
import { loadFont as loadInter } from "@remotion/google-fonts/Inter";
import { useVideoConfig } from "remotion";

export const { fontFamily: headFont } = loadPoppins("normal", {
  weights: ["600", "700", "800"],
  subsets: ["latin"],
});
export const { fontFamily: bodyFont } = loadInter("normal", {
  weights: ["400", "500", "600", "700"],
  subsets: ["latin"],
});

// The app's own palette (Resources/Styles/Colors.xaml) and the icon's indigo gradient, so
// the videos match the store screenshots.
export const C = {
  primary: "#6C63FF",
  primaryLight: "#8A7CFF",
  primaryDark: "#4326B0",
  accent: "#12D4A0",
  accentLight: "#3DE0B4",
  success: "#12D4A0",
  danger: "#FF6B6B",
  warning: "#FFB347",
  bg: "#F2F0FF",
  screen: "#0D0D1A",
  ink: "#241C5E",
  inkSoft: "#6B6899",
  white: "#FFFFFF",
  night: "#0D0D1A",
  /** Caption highlight on dark/indigo backgrounds. */
  hi: "#3DE0B4",
  /** Caption highlight on light backgrounds (darker mint for contrast). */
  hiDark: "#0E9C78",
};

// Recorded on the iPhone 17 Pro Max simulator.
export const FOOTAGE = { w: 1320, h: 2868 };

export type Layout = {
  landscape: boolean;
  w: number;
  h: number;
  /** Rectangle the phone footage occupies. */
  phone: { x: number; y: number; w: number; h: number };
  /** Rectangle captions sit in. */
  caption: { x: number; y: number; w: number; h: number };
  /** Base unit for type: 1 at 1080 wide portrait. */
  u: number;
};

/**
 * One layout rule for every export size, so a video is written once:
 * portrait (1080×1920 social, 886×1920 App Store) puts the caption above the phone,
 * landscape (1920×1080) puts it to the left.
 */
export const useLayout = (): Layout => {
  const { width: w, height: h } = useVideoConfig();
  const landscape = w > h;
  if (landscape) {
    const ph = h - 100;
    const pw = (ph * FOOTAGE.w) / FOOTAGE.h;
    const px = w - pw - 170;
    return {
      landscape, w, h, u: 1,
      phone: { x: px, y: 50, w: pw, h: ph },
      caption: { x: 130, y: 0, w: px - 260, h },
    };
  }
  const ph = 1450;
  const pw = (ph * FOOTAGE.w) / FOOTAGE.h;
  return {
    landscape, w, h, u: Math.min(1, w / 1080),
    phone: { x: (w - pw) / 2, y: h - ph - 70, w: pw, h: ph },
    caption: { x: 60, y: 70, w: w - 120, h: h - ph - 70 - 90 },
  };
};
