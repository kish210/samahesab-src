import { apiGet, apiPost } from './client';

/** ریلیزهای GitHub (از API سرور خودمان — توکن هرگز به کلاینت نمی‌رسد). */
export interface GitHubRelease {
  tagName: string;
  name: string | null;
  body: string | null;
  publishedAt: string | null;
  htmlUrl: string | null;
}

export function fetchGitHubReleases(): Promise<GitHubRelease[]> {
  return apiGet<GitHubRelease[]>('/api/github/releases');
}

/** ثبتِ گزارشِ باگ به‌صورت Issue در مخزنِ GitHub (سرور توکن را اعمال می‌کند). */
export async function submitGitHubIssue(title: string, description: string): Promise<{ url: string; message: string }> {
  return apiPost<{ url: string; message: string }>('/api/github/issues', { title, description });
}
