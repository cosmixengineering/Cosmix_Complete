export type AccessWorkspaceTab = 'roles' | 'permissions' | 'assignments';

export function initialAccessTab(pathname: string): AccessWorkspaceTab {
  return /^\/admin\/users\/?$/.test(pathname) ? 'assignments' : 'roles';
}
