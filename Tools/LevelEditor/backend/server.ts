import Fastify from "fastify";
import { randomBytes } from "node:crypto";
import path from "node:path";
import { Workspace } from "./workspace";
import { Game } from "./game";
import { PageLifetime } from "./page-lifetime";
import { embeddedFiles } from "./embedded";
import { previewImage } from "./image";
import type { Project } from "../shared/level";
export function createServer(
  workspace: Workspace,
  options: {
    dev?: boolean;
    frontendPort?: number;
    executable?: string;
    onShutdown?: () => void;
    onExit?: () => void;
  } = {},
) {
  const app = Fastify({
    bodyLimit: 16 * 1024 * 1024,
    forceCloseConnections: true,
  });
  const token = randomBytes(24).toString("hex"),
    game = new Game(workspace, options.executable);
  const autoShutdown = !!options.onShutdown;
  let busy = 0;
  const lifetime = options.onShutdown
    ? new PageLifetime(options.onShutdown, () => busy > 0 || workspace.saving)
    : undefined;
  app.addHook("preClose", async () => {
    lifetime?.dispose();
    await game.stop();
  });
  app.addHook("onRequest", async (req, reply) => {
    reply
      .header("Cache-Control", "no-store")
      .header("X-Content-Type-Options", "nosniff")
      .header(
        "Content-Security-Policy",
        "frame-ancestors 'none'; base-uri 'self'",
      );
    if (!["127.0.0.1", "localhost", "[::1]"].includes(req.hostname))
      return reply.code(403).send({ error: "Local access only" });
    if (
      req.url.startsWith("/api/") &&
      (req.headers["sec-fetch-site"] === "cross-site" ||
        (!["GET", "HEAD"].includes(req.method) &&
          req.headers["x-level-editor-token"] !== token))
    )
      return reply
        .code(403)
        .send({ error: "Reload the editor to renew your session" });
  });
  app.setErrorHandler((error, _req, reply) =>
    reply
      .code((error as { statusCode?: number }).statusCode || 400)
      .send({ error: (error as Error).message }),
  );
  app.get("/api/session", async (_req, reply) => {
    reply.header(
      "Set-Cookie",
      `level-editor-session=${token}; HttpOnly; SameSite=Strict; Path=/api/lifecycle`,
    );
    return {
      app: "CastleFrenzyLevelEditor",
      root: workspace.root,
      assets: workspace.defaultAssets,
      token,
      development: !!options.dev,
      autoShutdownOnClose: autoShutdown,
    };
  });
  app.get("/api/lifecycle", async (req, reply) => {
    if (!lifetime)
      return reply.code(503).send({ error: "Auto shutdown disabled" });
    if (
      !req.headers.cookie
        ?.split(";")
        .some((c) => c.trim() === `level-editor-session=${token}`)
    )
      return reply.code(403).send({ error: "Reload the editor" });
    reply.hijack();
    lifetime.connect(reply.raw);
  });
  app.get<{ Querystring: { assets?: string } }>("/api/project", (req) =>
    workspace.open(req.query.assets || workspace.defaultAssets),
  );
  app.post<{ Body: { assets: string; project: Project; revision: string } }>(
    "/api/save",
    (req) =>
      workspace.save(req.body.assets, req.body.project, req.body.revision),
  );
  app.get<{ Querystring: { assets: string; name: string } }>(
    "/api/model",
    (req) => workspace.model(req.query.assets, req.query.name),
  );
  app.get<{ Querystring: { path: string } }>(
    "/api/model-file",
    async (req, reply) =>
      reply
        .type("application/octet-stream")
        .send(await workspace.modelFile(req.query.path)),
  );
  app.get<{ Querystring: { path: string } }>(
    "/api/texture",
    async (req, reply) => {
      const { bytes } = await workspace.texture(req.query.path);
      return reply.type("image/png").send(previewImage(bytes));
    },
  );
  app.get("/api/game", () => game.status());
  app.post<{
    Body: { command: string; assets: string; level?: number; day?: number };
  }>("/api/game", async (req) => {
    busy++;
    try {
      const { command, assets, level = 0, day = 0 } = req.body;
      if (command === "stop") {
        await game.stop();
        return game.status();
      }
      if (!["play", "pause", "resume", "die", "overlay"].includes(command))
        throw new Error("Unknown game command");
      if (command === "play") {
        const opened = await workspace.open(assets);
        if (
          !Number.isInteger(level) ||
          !Number.isInteger(day) ||
          !opened.project.levels[level]?.days[day]
        )
          throw new Error("Choose a level and day to play");
        await game.start(assets);
      }
      return await game.command(command, level, day);
    } finally {
      busy--;
    }
  });
  app.post("/api/exit", async (_req, reply) => {
    await game.detach();
    reply.send({ ok: true });
    setImmediate(() => (options.onExit ?? options.onShutdown)?.());
  });
  app.setNotFoundHandler((req, reply) => {
    if (req.url === "/api" || req.url.startsWith("/api/"))
      return reply.code(404).send({ error: "Endpoint not found" });
    if (options.dev)
      return reply.redirect(`http://127.0.0.1:${options.frontendPort || 5191}`);
    const pathname = req.url.split("?")[0],
      file =
        embeddedFiles[pathname] ||
        (!path.posix.extname(pathname)
          ? embeddedFiles["/index.html"]
          : undefined);
    if (!file) return reply.code(404).send({ error: "File not found" });
    return reply.type(file.mime).send(Buffer.from(file.base64, "base64"));
  });
  return app;
}
