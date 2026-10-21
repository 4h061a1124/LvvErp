import { useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { useId, useRef, useState, type ReactNode } from 'react'
import { useDebouncedValue } from '@/lib/hooks'

interface AsyncComboboxProps<T> {
  id?: string
  value: T | null
  onChange: (value: T | null) => void
  /** Cache key prefix for the search query. */
  queryKey: string
  search: (term: string) => Promise<T[]>
  getKey: (item: T) => string
  /** Text shown in the box for the selected item. */
  getLabel: (item: T) => string
  renderOption: (item: T) => ReactNode
  /**
   * Item to take straight away on Enter, e.g. an exact code match. Barcode scanners type the code
   * and press Enter faster than the debounced search, so Enter runs the search immediately.
   */
  exactMatch?: (items: T[], term: string) => T | undefined
  /** Clear the box after picking (for "scan to add" inputs). */
  clearOnSelect?: boolean
  placeholder?: string
  noMatchesText: string
  /** Accessible name when no <label> points at the input. */
  ariaLabel?: string
  invalid?: boolean
  autoFocus?: boolean
  className?: string
}

export function AsyncCombobox<T>({
  id,
  value,
  onChange,
  queryKey,
  search,
  getKey,
  getLabel,
  renderOption,
  exactMatch,
  clearOnSelect,
  placeholder,
  noMatchesText,
  ariaLabel,
  invalid,
  autoFocus,
  className,
}: AsyncComboboxProps<T>) {
  const queryClient = useQueryClient()
  const listId = useId()
  const input = useRef<HTMLInputElement>(null)
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [highlight, setHighlight] = useState(0)
  const term = useDebouncedValue(text.trim(), 200)

  const results = useQuery({
    queryKey: [queryKey, 'search', term],
    queryFn: () => search(term),
    enabled: open,
    staleTime: 30_000,
  })
  const items = results.data ?? []

  function pick(item: T) {
    onChange(item)
    setOpen(false)
    setText('')
    setHighlight(0)
    if (clearOnSelect) input.current?.focus()
    else input.current?.blur()
  }

  async function onEnter() {
    const typed = text.trim()
    if (exactMatch && typed) {
      const found = await queryClient.fetchQuery({ queryKey: [queryKey, 'search', typed], queryFn: () => search(typed) })
      const match = exactMatch(found, typed)
      if (match) return pick(match)
    }
    if (items[highlight]) pick(items[highlight])
  }

  const shown = open || clearOnSelect ? text : value ? getLabel(value) : ''

  return (
    <div className={clsx('relative', className)}>
      <input
        ref={input}
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-label={ariaLabel}
        aria-invalid={invalid || undefined}
        autoComplete="off"
        autoFocus={autoFocus}
        value={shown}
        placeholder={value && !clearOnSelect ? getLabel(value) : placeholder}
        onFocus={() => {
          setOpen(true)
          setText('')
        }}
        onBlur={() => setOpen(false)}
        onChange={(e) => {
          setText(e.target.value)
          setHighlight(0)
          setOpen(true)
        }}
        onKeyDown={(e) => {
          if (e.key === 'ArrowDown') {
            e.preventDefault()
            setHighlight((h) => Math.min(h + 1, items.length - 1))
          } else if (e.key === 'ArrowUp') {
            e.preventDefault()
            setHighlight((h) => Math.max(h - 1, 0))
          } else if (e.key === 'Enter') {
            e.preventDefault()
            void onEnter()
          } else if (e.key === 'Escape') {
            setOpen(false)
          }
        }}
        className={clsx(
          'block h-8 w-full rounded-[3px] border bg-white px-2.5 text-sm text-zinc-900 placeholder:text-zinc-400',
          'focus:border-brand-500 focus:ring-2 focus:ring-brand-500/15 focus:outline-none',
          invalid ? 'border-red-500' : 'border-zinc-300',
        )}
      />
      {open && (
        <ul
          id={listId}
          role="listbox"
          className="absolute top-full z-30 mt-1 max-h-64 w-full min-w-64 overflow-auto rounded-[3px] border border-zinc-200 bg-white py-1 shadow-lg"
        >
          {results.isFetching && items.length === 0 ? (
            <li className="px-3 py-1.5 text-[13px] text-zinc-500">…</li>
          ) : items.length === 0 ? (
            <li className="px-3 py-1.5 text-[13px] text-zinc-500">{noMatchesText}</li>
          ) : (
            items.map((item, i) => (
              <li
                key={getKey(item)}
                role="option"
                aria-selected={i === highlight}
                // mousedown, not click: fires before the input's blur closes the list
                onMouseDown={(e) => {
                  e.preventDefault()
                  pick(item)
                }}
                onMouseEnter={() => setHighlight(i)}
                className={clsx('cursor-pointer px-3 py-1.5 text-sm', i === highlight ? 'bg-brand-50' : 'hover:bg-zinc-50')}
              >
                {renderOption(item)}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}
