const express = require('express');
const fs = require('fs');
const path = require('path');
const sqlite3 = require('sqlite3');
const { open } = require('sqlite');
const cors = require('cors');
const { apiReference } = require('@scalar/express-api-reference');
const { OpenAPIBackend } = require('openapi-backend');
const carregarControladores = require('./controllers');
const { CAMPOS_BOOLEANOS_GLOBAIS, coletarCamposBooleanos } = require('./utils/normalizadores');

async function startServer() {
  const db = await open({
    filename: path.join(__dirname, 'logicpos.db'),
    driver: sqlite3.Database
  });
  console.log('💾 Conectado à base de dados SQLite (logicpos.db).');

  const apiSpec = JSON.parse(fs.readFileSync(path.join(__dirname, 'openapi.json'), 'utf8'));
  Object.values(apiSpec.components.schemas || {}).forEach(schema =>
    coletarCamposBooleanos(schema, apiSpec.components.schemas)
  );
  console.log(`🧩 [BOOLEANOS] ${CAMPOS_BOOLEANOS_GLOBAIS.size} nomes de campo booleano carregados do contrato.`);

  const app = express();
  app.use(cors());
  app.use(express.json());
  app.get('/openapi.json', (req, res) => res.json(apiSpec));
  app.use('/docs', apiReference({ spec: { url: '/openapi.json' } }));

  const controladores = carregarControladores({ db, apiSpec });
  const { registarRotas, tokensAutorizados, ...operacoes } = controladores;
  registarRotas(app);

  const api = new OpenAPIBackend({ definition: apiSpec, validate: false, quick: true });
  api.register({
    ...operacoes,
    unauthorizedHandler: (contexto, req, res) => res.status(401).json({ title: 'Unauthorized', status: 401 }),
    notFound: (contexto, req, res) => res.status(404).json({ title: 'Not Found', status: 404 })
  });
  api.registerSecurityHandler('JWTBearerAuth', (contexto, req) => {
    const autorizacao = req.headers.authorization;
    const correspondencia = /^Bearer (\S+)$/i.exec(autorizacao || '');
    return correspondencia && tokensAutorizados.has(correspondencia[1]);
  });
  await api.init();
  app.use((req, res, next) => api.handleRequest(req, req, res).catch(next));

  const PORT = Number(process.env.PORT || 5001);
  app.listen(PORT, () => {
    console.log(`\n🚀 Servidor emulado em http://localhost:${PORT}.`);
  });
}

startServer().catch(console.error);