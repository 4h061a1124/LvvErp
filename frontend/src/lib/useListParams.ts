import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'

type ParamValues = Record<string, string | number>

/**
 * List filters kept in the URL query string, so a filtered list can be bookmarked or shared.
 * Keys of <paramref name="defaults"/> are the query parameter names; values equal to the default
 * are left out of the URL. Changing anything but <c>page</c> resets <c>page</c> to its default.
 *
 * Pass a module-level constant for <paramref name="defaults"/> (a new object each render would
 * recompute the values every time).
 */
export function useListParams<T extends ParamValues>(defaults: T) {
  const [params, setParams] = useSearchParams()

  const values = useMemo(() => {
    const result: ParamValues = { ...defaults }
    for (const key of Object.keys(defaults)) {
      const raw = params.get(key)
      if (raw === null) continue
      result[key] = typeof defaults[key] === 'number' ? Number(raw) || defaults[key] : raw
    }
    return result as T
  }, [params, defaults])

  const update = useCallback(
    (patch: Partial<T>) => {
      setParams(
        (prev) => {
          const merged: ParamValues = { ...values, ...patch }
          if ('page' in defaults && !('page' in patch)) merged.page = defaults.page
          const next = new URLSearchParams(prev)
          for (const key of Object.keys(defaults)) {
            if (merged[key] === defaults[key] || merged[key] === '') next.delete(key)
            else next.set(key, String(merged[key]))
          }
          return next
        },
        { replace: true },
      )
    },
    [values, setParams, defaults],
  )

  return [values, update] as const
}
