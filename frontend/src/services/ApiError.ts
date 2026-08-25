import type { ApiProblem } from "../types";

export class ApiError extends Error {
  readonly problem: ApiProblem;

  constructor(problem: ApiProblem) {
    super(problem.detail);
    this.name = "ApiError";
    this.problem = problem;
  }
}
