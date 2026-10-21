import clsx from 'clsx'
import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'dangerGhost'
type Size = 'sm' | 'md'

const variants: Record<Variant, string> = {
  primary: 'bg-brand-600 text-white hover:bg-brand-700 active:bg-brand-800 border border-brand-700',
  secondary: 'bg-white text-zinc-800 border border-zinc-300 hover:bg-zinc-50 active:bg-zinc-100',
  ghost: 'text-zinc-700 hover:bg-zinc-100 active:bg-zinc-200',
  danger: 'bg-red-600 text-white border border-red-700 hover:bg-red-700',
  dangerGhost: 'text-red-700 hover:bg-red-50 active:bg-red-100',
}

const sizes: Record<Size, string> = {
  sm: 'h-7 px-2.5 text-[13px]',
  md: 'h-8 px-3 text-sm',
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
}

export function Button({ variant = 'secondary', size = 'md', className, type = 'button', ...props }: ButtonProps) {
  return (
    <button
      type={type}
      className={clsx(
        'inline-flex shrink-0 items-center justify-center gap-1.5 rounded-[3px] font-medium whitespace-nowrap select-none',
        'disabled:cursor-not-allowed disabled:opacity-55',
        variants[variant],
        sizes[size],
        className,
      )}
      {...props}
    />
  )
}
