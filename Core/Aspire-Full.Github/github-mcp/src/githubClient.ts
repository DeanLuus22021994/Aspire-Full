import { Octokit } from "@octokit/core";
import type { Config } from "./config.js";

type MinimalWorkflowRun = {
  name?: string | null;
  status?: string | null;
  conclusion?: string | null;
  html_url?: string | null;
  updated_at?: string | null;
  created_at?: string | null;
};

type MinimalIssue = {
  number: number;
  title: string;
  html_url: string;
  labels?: Array<string | { name?: string | null }>;
  created_at?: string;
  comments?: number;
  pull_request?: object;
};

export interface RepoSummary {
  openIssues: number;
  openPullRequests: number;
  defaultBranch: string;
  latestRelease?: string;
  stars: number;
  forks: number;
  watchers: number;
}

export interface WorkflowStatus {
  workflow: string;
  status: string;
  conclusion: string | null;
  htmlUrl: string;
  updatedAt: string;
}

export interface IssueInsight {
  number: number;
  title: string;
  url: string;
  labels: string[];
  createdAt: string;
  comments: number;
}

export class GitHubClient {
  private readonly octokit: Octokit;
  private readonly owner: string;
  private readonly repo: string;

  constructor(private readonly config: Config) {
    this.octokit = new Octokit({ auth: config.githubToken });
    const [owner, repo] = config.repository.split("/");
    this.owner = owner;
    this.repo = repo;
  }

  async getRepoSummary(): Promise<RepoSummary> {
    const { data } = await this.octokit.request("GET /repos/{owner}/{repo}", {
      owner: this.owner,
      repo: this.repo
    });

    const releases = await this.octokit.request("GET /repos/{owner}/{repo}/releases", {
      owner: this.owner,
      repo: this.repo,
      per_page: 1
    });

    return {
      openIssues: data.open_issues_count ?? 0,
      openPullRequests: await this.countPullRequests(),
      defaultBranch: data.default_branch,
      latestRelease: releases.data[0]?.tag_name,
      stars: data.stargazers_count,
      forks: data.forks_count,
      watchers: data.subscribers_count
    };
  }

  private async countPullRequests(): Promise<number> {
    const response = await this.octokit.request("GET /repos/{owner}/{repo}/pulls", {
      owner: this.owner,
      repo: this.repo,
      state: "open",
      per_page: 1
    });

    const total = Number(response.headers["x-total-count"] ?? response.data.length);
    return Number.isNaN(total) ? response.data.length : total;
  }

  async getWorkflowStatuses(limit = 3): Promise<WorkflowStatus[]> {
    const { data } = await this.octokit.request("GET /repos/{owner}/{repo}/actions/runs", {
      owner: this.owner,
      repo: this.repo,
      per_page: limit
    });

    const runs = (data.workflow_runs ?? []) as MinimalWorkflowRun[];
    return runs.map((run: MinimalWorkflowRun) => ({
      workflow: run.name ?? "workflow",
      status: run.status ?? "unknown",
      conclusion: run.conclusion ?? null,
      htmlUrl: run.html_url ?? "",
      updatedAt: run.updated_at ?? run.created_at ?? new Date().toISOString()
    }));
  }

  async getIssueInsights(limit = 5): Promise<IssueInsight[]> {
    const { data } = await this.octokit.request("GET /repos/{owner}/{repo}/issues", {
      owner: this.owner,
      repo: this.repo,
      state: "open",
      per_page: limit
    });

    const issues = data as MinimalIssue[];
    return issues
      .filter((item: MinimalIssue) => !("pull_request" in item))
      .map((issue: MinimalIssue) => ({
        number: issue.number,
        title: issue.title,
        url: issue.html_url,
        labels:
          issue.labels?.map((label: string | { name?: string | null }) =>
            typeof label === "string" ? label : label.name ?? ""
          ) ?? [],
        createdAt: issue.created_at ?? new Date().toISOString(),
        comments: issue.comments ?? 0
      }));
  }
}
