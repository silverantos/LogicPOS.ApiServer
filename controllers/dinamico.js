const { resolverTabela, resolverSchemaResposta, responderSemTabela, ajustarRespostaAoSchema } = require('../utils/motorDinamico');
const { normalizarParaContrato } = require('../utils/normalizadores');

module.exports = ({ db, apiSpec }) => ({
  notImplemented: async (contexto, req, res) => {
    const { path: caminhoOriginal, method: metodo } = contexto.operation;
    try {
      if (metodo.toLowerCase() !== 'get') {
        return res.status(200).json({ isSuccess: true, errors: [] });
      }

      const tabela = await resolverTabela(db, caminhoOriginal);
      if (!tabela) {
        responderSemTabela(caminhoOriginal, res, apiSpec, metodo.toLowerCase());
        return;
      }

      const paramsChaves = Object.keys(contexto.request.params || {});
      if (paramsChaves.length > 0) {
        const valorParam = contexto.request.params[paramsChaves[0]];
        const linha = await db.get(`SELECT * FROM "${tabela}" WHERE id = ?`, [valorParam]);
        return linha ? res.json(normalizarParaContrato(linha)) : res.status(404).json({ title: 'Not Found', status: 404 });
      }

      const linhas = await db.all(`SELECT * FROM "${tabela}" LIMIT 150`);
      const dadosTratados = normalizarParaContrato(linhas);
      const schemaResposta = resolverSchemaResposta(apiSpec, caminhoOriginal, metodo.toLowerCase());
      const parametrosPaginacao = ['page', 'pagenumber', 'pagesize', 'limit', 'offset'];
      const usaPaginacao = Object.keys(req.query || {}).some(chave => parametrosPaginacao.includes(chave.toLowerCase()));
      const respostaCompativel = ajustarRespostaAoSchema(dadosTratados, schemaResposta, apiSpec);

      if (usaPaginacao && Array.isArray(dadosTratados)) {
        return res.json({
          items: dadosTratados,
          itemsCount: dadosTratados.length,
          totalItems: dadosTratados.length,
          page: Number(req.query.page || req.query.pageNumber || 1),
          pageSize: Number(req.query.pageSize || req.query.limit || 150),
          totalPages: 1
        });
      }
      return res.json(respostaCompativel);
    } catch (err) {
      console.error(`Erro ao processar ${metodo.toUpperCase()} ${caminhoOriginal}:`, err.message);
      if (!res.headersSent) res.status(500).json({ title: 'Internal Server Error', status: 500 });
    }
  }
});