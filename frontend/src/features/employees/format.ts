/** Vietnamese names put the given name last: "Nguyễn Văn Đức" -> "Đ". */
export function initialOf(fullName: string): string {
  const parts = fullName.trim().split(/\s+/)
  return (parts[parts.length - 1]?.charAt(0) ?? '').toUpperCase()
}
