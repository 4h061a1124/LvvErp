import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { CreateUserInput, UpdateUserInput, User } from './types'

export const userKeys = {
  all: ['users'] as const,
}

/** All accounts; the list is small (one per person in the factory), so it is filtered in the browser. */
export function useUsers() {
  return useQuery({
    queryKey: userKeys.all,
    queryFn: async () => (await api.get<User[]>('/users')).data,
  })
}

export function useSaveUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (args: { id: string; input: UpdateUserInput } | { id?: undefined; input: CreateUserInput }) =>
      args.id ? (await api.put<User>(`/users/${args.id}`, args.input)).data : (await api.post<User>('/users', args.input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: userKeys.all }),
  })
}
