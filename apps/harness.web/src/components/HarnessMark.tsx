type Variant = "orange" | "purple" | "cyan" | "duo";

type Size = "xs" | "sm" | "md" | "lg" | "xl";

const sizeClass: Record<Size, string> = {
  xs: "w-5 h-5",
  sm: "w-6 h-6",
  md: "w-8 h-8",
  lg: "w-10 h-10",
  xl: "w-14 h-14",
};

export interface HarnessMarkProps {
  size?: Size;
  variant?: Variant;
  pulse?: boolean;
  className?: string;
  title?: string;
}

/**
 * CustomAgentHarness mark.
 *
 * Procedural SVG. Two concentric hexagons (outer/inner), a circuit "tee" on
 * each cardinal axis, and a center node. Designed to read as a chip / die /
 * substrate from a distance, with circuit detail visible on close inspection.
 *
 * Variants pick the gradient stops; "duo" is the canonical orange→purple sweep
 * used in the marketing surface; "orange" / "purple" / "cyan" are mono variants
 * suited for chrome where another color carries the page identity.
 */
export function HarnessMark({
  size = "lg",
  variant = "duo",
  pulse = false,
  className = "",
  title,
}: HarnessMarkProps) {
  const stops = stopsForVariant(variant);
  const gid = `hm-grad-${variant}`;
  const fid = `hm-glow-${variant}`;
  return (
    <svg
      viewBox="0 0 64 64"
      className={`${sizeClass[size]} ${className}`}
      role={title ? "img" : "presentation"}
      aria-hidden={title ? undefined : true}
      aria-label={title}
    >
      <defs>
        <linearGradient id={gid} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor={stops[0]} />
          <stop offset="100%" stopColor={stops[1]} />
        </linearGradient>
        <radialGradient id={fid} cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor={stops[1]} stopOpacity="0.55" />
          <stop offset="60%" stopColor={stops[0]} stopOpacity="0.10" />
          <stop offset="100%" stopColor={stops[0]} stopOpacity="0" />
        </radialGradient>
      </defs>

      {/* soft outer glow */}
      <circle cx="32" cy="32" r="28" fill={`url(#${fid})`} />

      {/* outer hex shell */}
      <polygon
        points="32,4 56,18 56,46 32,60 8,46 8,18"
        fill="none"
        stroke={`url(#${gid})`}
        strokeWidth="2"
        strokeLinejoin="miter"
      />

      {/* inner hex */}
      <polygon
        points="32,16 47,24.5 47,39.5 32,48 17,39.5 17,24.5"
        fill="none"
        stroke={`url(#${gid})`}
        strokeWidth="1.25"
        opacity="0.55"
      />

      {/* circuit traces — N/S/E/W tees */}
      <g stroke={`url(#${gid})`} strokeWidth="1.25" opacity="0.85">
        <line x1="32" y1="4"  x2="32" y2="16" />
        <line x1="32" y1="48" x2="32" y2="60" />
        <line x1="8"  y1="32" x2="17" y2="32" />
        <line x1="47" y1="32" x2="56" y2="32" />
      </g>

      {/* circuit termination pads */}
      <g fill={stops[0]} opacity="0.9">
        <rect x="30.5" y="2.5"  width="3" height="3" />
        <rect x="30.5" y="58.5" width="3" height="3" />
      </g>
      <g fill={stops[1]} opacity="0.9">
        <rect x="2.5"  y="30.5" width="3" height="3" />
        <rect x="58.5" y="30.5" width="3" height="3" />
      </g>

      {/* center node */}
      <g className={pulse ? "harness-mark-pulse" : undefined}>
        <circle cx="32" cy="32" r="5" fill={`url(#${gid})`} />
        <circle
          cx="32"
          cy="32"
          r="8"
          fill="none"
          stroke={`url(#${gid})`}
          strokeWidth="0.75"
          opacity="0.6"
        />
      </g>
      {title ? <title>{title}</title> : null}
    </svg>
  );
}

function stopsForVariant(v: Variant): [string, string] {
  switch (v) {
    case "orange":
      return ["#FF8C00", "#FFAE3D"];
    case "purple":
      return ["#B026FF", "#CD66FF"];
    case "cyan":
      return ["#00F0FF", "#67F7FF"];
    case "duo":
    default:
      return ["#FF8C00", "#B026FF"];
  }
}
