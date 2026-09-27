function deduzirTabela(caminhoRota) {
  const segmentos = caminhoRota.toLowerCase().split('/').filter(Boolean);
  if (segmentos.length === 0) return null;
  const prefixosModulo = ['system', 'licensing', 'database', 'reports', 'payment', 'articles', 'at', 'agt', 'company', 'worksessions', 'fiscal-years', 'users', 'auth'];
  if (prefixosModulo.includes(segmentos[0])) {
    return segmentos[1] ? segmentos[1].replace(/-/g, '_') : segmentos[0].replace(/-/g, '_');
  }
  return segmentos[0].replace(/-/g, '_');
}

function normalizarNomeTabela(nome) {
  return nome.toLowerCase().replace(/[^a-z0-9]/g, '');
}

async function resolverTabela(db, caminhoRota) {
  const tabelas = await db.all("SELECT name FROM sqlite_master WHERE type = 'table'");
  const nomesDisponiveis = tabelas.map(({ name }) => ({
    name,
    normalizado: normalizarNomeTabela(name)
  }));
  const segmentos = caminhoRota.split('/').filter(Boolean)
    .filter(segmento => !/^\{[^}]+\}$/.test(segmento));
  const partes = segmentos.map(segmento => normalizarNomeTabela(segmento));
  const candidatos = [];

  for (let inicio = 0; inicio < partes.length; inicio += 1) {
    for (let fim = inicio + 1; fim <= partes.length; fim += 1) {
      candidatos.push(partes.slice(inicio, fim).join(''));
    }
  }

  const encontrados = nomesDisponiveis.filter(tabela =>
    candidatos.some(candidato => candidato === tabela.normalizado)
  );
  if (encontrados.length > 0) {
    return encontrados.sort((a, b) => b.normalizado.length - a.normalizado.length)[0].name;
  }

  const tabelaDeduzida = deduzirTabela(caminhoRota);
  if (!tabelaDeduzida) return null;
  return nomesDisponiveis.find(tabela =>
    tabela.normalizado === normalizarNomeTabela(tabelaDeduzida)
  )?.name || null;
}

function resolverSchemaResposta(apiSpec, caminhoRota, metodo) {
  const operacao = apiSpec.paths[caminhoRota]?.[metodo];
  const resposta = operacao?.responses?.['200'] || operacao?.responses?.['201'];
  return resposta?.content?.['application/json']?.schema || null;
}

function resolverSchema(schema, apiSpec) {
  if (!schema?.$ref) return schema;
  return apiSpec.components.schemas[schema.$ref.split('/').pop()] || null;
}

function valorPorSchema(schema, apiSpec, visitados = new Set()) {
  const resolvido = resolverSchema(schema, apiSpec);
  if (!resolvido || visitados.has(resolvido)) return {};
  visitados.add(resolvido);
  if (resolvido.type === 'array') return [];
  if (resolvido.type === 'boolean') return false;
  if (resolvido.type === 'integer' || resolvido.type === 'number') return 0;
  if (resolvido.type === 'string') return '';
  if (resolvido.allOf) {
    return Object.assign({}, ...resolvido.allOf.map(parte => valorPorSchema(parte, apiSpec, visitados)));
  }
  if (resolvido.type === 'object' || resolvido.properties) {
    const objeto = {};
    for (const [nome, propriedade] of Object.entries(resolvido.properties || {})) {
      objeto[nome] = valorPorSchema(propriedade, apiSpec, new Set(visitados));
    }
    return objeto;
  }
  return {};
}

function responderSemTabela(caminhoRota, res, apiSpec, metodo) {
  const caminho = caminhoRota.toLowerCase();
  if (caminho.endsWith('/pdf')) {
    res.type('application/pdf').send('%PDF-1.4\n% API Server\n');
    return;
  }
  if (/(^|\/)(name|api-version)$/.test(caminho)) {
    res.json('');
    return;
  }
  if (/(is-open|is-printed|has-active|was-printed)/.test(caminho)) {
    res.json(false);
    return;
  }
  const schema = resolverSchemaResposta(apiSpec, caminhoRota, metodo);
  res.json(valorPorSchema(schema, apiSpec));
}

function ajustarRespostaAoSchema(dados, schema, apiSpec) {
  const resolvido = resolverSchema(schema, apiSpec);
  if (!resolvido) return dados;
  if (resolvido.type === 'array') return Array.isArray(dados) ? dados : [];
  if (resolvido.type === 'object' || resolvido.properties || resolvido.allOf) {
    const propriedades = resolvido.properties || {};
    if (propriedades.items && Array.isArray(dados)) {
      return {
        ...valorPorSchema(resolvido, apiSpec),
        items: dados,
        itemsCount: dados.length,
        totalItems: dados.length,
        page: 1,
        pageSize: 150,
        totalPages: 1
      };
    }
    return Array.isArray(dados) ? (dados[0] || valorPorSchema(resolvido, apiSpec)) : dados;
  }
  if (resolvido.type === 'boolean') return false;
  if (resolvido.type === 'integer' || resolvido.type === 'number') return 0;
  if (resolvido.type === 'string') return '';
  return dados;
}

module.exports = { resolverTabela, resolverSchemaResposta, responderSemTabela, ajustarRespostaAoSchema };