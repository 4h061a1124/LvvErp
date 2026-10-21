import { Package } from 'lucide-react'

export function MaterialThumb({ photoUrl, size = 32 }: { photoUrl: string | null; size?: number }) {
  const style = { width: size, height: size }
  if (photoUrl) {
    return (
      <img
        src={photoUrl}
        alt=""
        style={style}
        loading="lazy"
        className="shrink-0 rounded-[3px] border border-zinc-200 bg-zinc-100 object-cover"
      />
    )
  }
  return (
    <span style={style} className="inline-flex shrink-0 items-center justify-center rounded-[3px] bg-zinc-100 text-zinc-400" aria-hidden="true">
      <Package size={Math.round(size * 0.5)} strokeWidth={1.5} />
    </span>
  )
}
