import clsx from 'clsx'
import { initialOf } from '../format'

export function EmployeeAvatar({
  name,
  photoUrl,
  size = 32,
  className,
}: {
  name: string
  photoUrl: string | null
  size?: number
  className?: string
}) {
  const style = { width: size, height: size }

  if (photoUrl) {
    return (
      <img
        src={photoUrl}
        alt=""
        style={style}
        loading="lazy"
        className={clsx('shrink-0 rounded-[3px] border border-zinc-200 bg-zinc-100 object-cover', className)}
      />
    )
  }

  return (
    <span
      style={{ ...style, fontSize: Math.round(size * 0.42) }}
      className={clsx(
        'inline-flex shrink-0 items-center justify-center rounded-[3px] bg-zinc-200 font-medium text-zinc-600',
        className,
      )}
      aria-hidden="true"
    >
      {initialOf(name)}
    </span>
  )
}
