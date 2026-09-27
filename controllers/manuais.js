const {
  normalizarParaContrato, normalizarFamiliaArtigo, normalizarSubfamiliaArtigo,
  normalizarMesaViewModel, normalizarArtigoViewModel, comoBooleano, comoNumero,
  criarNormalizadores
} = require('../utils/normalizadores');

const TOKEN_JWT = 'MOCK_JWT_TOKEN';
const TOKEN_TERMINAL = 'MOCK_TERMINAL_TOKEN';

module.exports = ({ db }) => ({
  tokensAutorizados: new Set([TOKEN_JWT, TOKEN_TERMINAL]),
  registarRotas(app) {
    const { SQL_ARTIGO_BASE, normalizarOrderViewModel } = criarNormalizadores(db);
    const REAL_TERMINAL_ID = '3C9EEBC8-5276-4C59-8FE9-E0D7DE722BBE';

    app.get('/users/:id/permissions', async (req, res) => {
      try {
        const linhas = await db.all(
          'SELECT Token FROM "PermissionItems" WHERE IsDeleted = 0 OR IsDeleted IS NULL'
        );
        res.json(linhas.map(linha => linha.Token));
      } catch (err) {
        res.json([]);
      }
    });

    const mockFiscalYearObj = {
      id: '99999999-9999-9999-9999-999999999999',
      year: 2026,
      startDate: '2026-01-01T00:00:00.000Z',
      endDate: '2026-12-31T23:59:59.000Z',
      isActive: true,
      isClosed: false,
      notes: 'Ano fiscal simulado'
    };

    const responderAnoFiscal = (req, res) => {
      res.json(normalizarParaContrato(mockFiscalYearObj));
    };
    app.get('/at/fiscal-years/current', responderAnoFiscal);
    app.get('/at/fiscalyears/current', responderAnoFiscal);
    app.get('/at/fiscal-years/active', responderAnoFiscal);
    app.get('/at/fiscalyears/active', responderAnoFiscal);
    app.get('/fiscal-years/current', responderAnoFiscal);

    app.get('/users/:id/name', async (req, res) => {
      try {
        const utilizador = await db.get('SELECT * FROM "Users" WHERE id = ?', [req.params.id]);
        if (!utilizador) return res.status(404).json({ title: 'Not Found', status: 404 });
        const nome = utilizador.Name || utilizador.name ||
          [utilizador.FirstName, utilizador.LastName].filter(Boolean).join(' ');
        return res.json(nome || '');
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/articles/families', async (req, res) => {
      try {
        const familias = await db.all(
          'SELECT * FROM "ArticleFamilies" WHERE IsDeleted = 0 OR IsDeleted IS NULL ORDER BY "Order", Designation'
        );
        return res.json(familias.map(normalizarFamiliaArtigo));
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/articles/subfamilies', async (req, res) => {
      try {
        const subfamilias = await db.all(
          'SELECT * FROM "ArticleSubfamilies" WHERE IsDeleted = 0 OR IsDeleted IS NULL ORDER BY "Order", Designation'
        );
        return res.json(subfamilias.map(normalizarSubfamiliaArtigo));
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/articles', async (req, res) => {
      try {
        let artigos = await db.all(`
          ${SQL_ARTIGO_BASE}
          WHERE a.IsDeleted = 0 OR a.IsDeleted IS NULL
          ORDER BY a."Order", a.Designation
        `);
        if (req.query.familyId) artigos = artigos.filter(artigo => artigo.FamilyId === req.query.familyId);
        if (req.query.subFamilyId) artigos = artigos.filter(artigo => artigo.SubfamilyId === req.query.subFamilyId);
        if (req.query.favorite !== undefined) {
          artigos = artigos.filter(artigo => comoBooleano(artigo.Favorite) === (req.query.favorite === 'true'));
        }
        if (req.query.search) {
          const pesquisa = String(req.query.search).toLowerCase();
          artigos = artigos.filter(artigo =>
            String(artigo.Designation || '').toLowerCase().includes(pesquisa) ||
            String(artigo.Code || '').toLowerCase().includes(pesquisa)
          );
        }
        const page = Math.max(Number.parseInt(req.query.page, 10) || 1, 1);
        const pageSize = Math.max(Number.parseInt(req.query.pageSize, 10) || 150, 1);
        const inicio = (page - 1) * pageSize;
        const items = artigos.slice(inicio, inicio + pageSize).map(normalizarArtigoViewModel);
        return res.json({
          items,
          itemsCount: items.length,
          totalItems: artigos.length,
          page,
          pageSize,
          totalPages: Math.max(Math.ceil(artigos.length / pageSize), 1)
        });
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/Orders', async (req, res) => {
      try {
        let orders = await db.all(
          'SELECT * FROM "Orders" WHERE IsDeleted = 0 OR IsDeleted IS NULL ORDER BY CreatedAt'
        );
        if (req.query.tableId) orders = orders.filter(order => order.TableId === req.query.tableId);
        const items = await Promise.all(orders.map(normalizarOrderViewModel));
        return res.json(items);
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/Orders/:id', async (req, res) => {
      try {
        const order = await db.get(
          'SELECT * FROM "Orders" WHERE Id = ? AND (IsDeleted = 0 OR IsDeleted IS NULL)',
          [req.params.id]
        );
        if (!order) return res.status(404).json({ title: 'Not Found', status: 404 });
        return res.json(await normalizarOrderViewModel(order));
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/reports/monthly-sales', async (req, res) => {
      try {
        const documentos = await db.all(
          'SELECT CreatedAt, TotalNet, TotalFinal FROM "Documents" WHERE IsDeleted = 0 OR IsDeleted IS NULL'
        );
        const porAno = new Map();
        for (const documento of documentos) {
          const data = new Date(String(documento.CreatedAt || '').trim().replace(' ', 'T'));
          if (Number.isNaN(data.getTime())) continue;
          const ano = data.getFullYear();
          const mes = data.getMonth() + 1;
          if (!porAno.has(ano)) porAno.set(ano, new Map());
          const porMes = porAno.get(ano);
          const totais = porMes.get(mes) || { netTotal: 0, finalTotal: 0 };
          totais.netTotal += comoNumero(documento.TotalNet);
          totais.finalTotal += comoNumero(documento.TotalFinal);
          porMes.set(mes, totais);
        }
        const anosDisponiveis = [...porAno.keys()].sort((a, b) => b - a);
        const anoPedido = Number.parseInt(req.query.year, 10);
        const ano = Number.isFinite(anoPedido) ? anoPedido : (anosDisponiveis[0] || new Date().getFullYear());
        const porMesDoAno = porAno.get(ano) || new Map();
        const sales = [...porMesDoAno.entries()]
          .sort((a, b) => a[0] - b[0])
          .map(([mes, totais]) => ({ month: mes, netTotal: totais.netTotal, finalTotal: totais.finalTotal }));
        return res.json({ year: ano, years: anosDisponiveis, sales: sales });
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/reports/sales-for-day', async (req, res) => {
      try {
        const documentos = await db.all(
          'SELECT CreatedAt, TotalFinal FROM "Documents" WHERE IsDeleted = 0 OR IsDeleted IS NULL'
        );
        const diaPedido = req.query.day ? new Date(`${req.query.day}T00:00:00`) : new Date();
        if (Number.isNaN(diaPedido.getTime())) return res.status(404).json({ title: 'Not Found', status: 404 });
        let dayTotal = 0;
        let monthTotal = 0;
        let yearTotal = 0;
        for (const documento of documentos) {
          const data = new Date(String(documento.CreatedAt || '').trim().replace(' ', 'T'));
          if (Number.isNaN(data.getTime())) continue;
          const total = comoNumero(documento.TotalFinal);
          if (data.getFullYear() === diaPedido.getFullYear()) {
            yearTotal += total;
            if (data.getMonth() === diaPedido.getMonth()) {
              monthTotal += total;
              if (data.getDate() === diaPedido.getDate()) dayTotal += total;
            }
          }
        }
        return res.json({
          day: `${diaPedido.getFullYear()}-${String(diaPedido.getMonth() + 1).padStart(2, '0')}-${String(diaPedido.getDate()).padStart(2, '0')}`,
          dayTotal: dayTotal,
          monthTotal: monthTotal,
          yearTotal: yearTotal
        });
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/worksessions/periods/terminal-is-open', (req, res) => res.json(false));

    app.get('/tables/default', async (req, res) => {
      try {
        const mesa = await db.get(`
          SELECT t.*, p.Designation AS PlaceDesignation
          FROM "Tables" t
          LEFT JOIN "Places" p ON p.Id = t.PlaceId
          WHERE t.IsDeleted = 0 OR t.IsDeleted IS NULL
          ORDER BY (CASE WHEN t.Status = 0 THEN 0 ELSE 1 END), t."Order"
          LIMIT 1
        `);
        if (!mesa) return res.status(404).json({ title: 'Not Found', status: 404 });
        return res.json(normalizarMesaViewModel(mesa));
      } catch (err) {
        return res.status(500).json({ title: 'Internal Server Error', status: 500 });
      }
    });

    app.get('/company/info', async (req, res) => {
      try {
        const params = await db.all('SELECT Token, Value FROM PreferenceParameters');
        const getParam = (token) => {
          const row = params.find(p => p.Token === token);
          return row ? row.Value : '';
        };
        const companyObj = {
          name: getParam('COMPANY_NAME') || 'Q.ta Moinhos da Ponte, Unipessoal Lda.',
          businessName: getParam('COMPANY_BUSINESS_NAME') || 'LogicPOS Trade',
          fiscalNumber: getParam('COMPANY_FISCALNUMBER') || '514270969',
          postalCode: getParam('COMPANY_POSTALCODE') || '3420-136',
          city: getParam('COMPANY_CITY') || 'Midões TBU',
          address: getParam('COMPANY_ADDRESS') || 'Estrada S. Geraldo',
          currencyCode: getParam('SYSTEM_CURRENCY') || 'EUR'
        };
        res.json(normalizarParaContrato(companyObj));
      } catch (err) {
        res.json(normalizarParaContrato({ name: 'Q.ta Moinhos da Ponte', fiscalNumber: '514270969', currencyCode: 'EUR' }));
      }
    });

    app.get('/licensing/system/lastest-version', (req, res) => res.json({ version: '1.5.2' }));
    app.get('/licensing/refresh', (req, res) => res.status(204).end());
    app.get('/system/api-version', (req, res) => res.json('1.5.2'));
    app.get('/licensing/hardware-id', (req, res) => res.json({ hardwareId: REAL_TERMINAL_ID }));
    app.get('/system/information', (req, res) => res.json({ culture: 'pt-PT', countryCode2: 'PT', module: 'Standard' }));

    app.get('/licensing/data', (req, res) => {
      res.json({
        data: {
          isLicensed: true, version: '1.5.2', hardwareId: REAL_TERMINAL_ID,
          status: 1, date: '2035-12-31T00:00:00.000Z', name: 'Licença Local', company: 'LogicPOS',
          nif: '514270969', address: 'Localhost', reseller: 'Scalar', stocksModule: true, agtFeModule: true, isValid: true
        }
      });
    });

    const mockTerminalObj = {
      id: REAL_TERMINAL_ID, code: 'T001', order: 1, designation: 'Terminal Principal LogicPOS',
      hardwareId: REAL_TERMINAL_ID, timerInterval: 5000, isDefault: true,
      placeId: '66666666-6666-6666-6666-666666666666', printerId: '22222222-2222-2222-2222-222222222222',
      thermalPrinterId: '22222222-2222-2222-2222-222222222222'
    };
    app.get('/terminals', (req, res) => res.json([normalizarParaContrato(mockTerminalObj)]));
    app.get('/terminals/:id', (req, res) => res.json(normalizarParaContrato(mockTerminalObj)));
    app.get('/terminals/hardwareid/:id', (req, res) => res.json(normalizarParaContrato(mockTerminalObj)));

    app.post('/auth/sign-in', (req, res) => res.json(TOKEN_JWT));
    app.post('/auth/login', (req, res) => res.json(TOKEN_TERMINAL));
  }
});