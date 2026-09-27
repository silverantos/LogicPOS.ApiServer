function normalizarFamiliaArtigo(linha) {
  return {
    id: linha.Id,
    code: linha.Code,
    order: linha.Order,
    designation: linha.Designation,
    commissionGroupId: linha.CommissionGroupId || null,
    discountGroupId: linha.DiscountGroupId || null,
    commissionGroup: null,
    discountGroup: null,
    button: {
      label: linha.Button_Label || null,
      image: linha.Button_Image || null,
      imageExtension: linha.Button_ImageExtension || null
    }
  };
}

function comoBooleano(valor) {
  return valor === true || valor === 1 || valor === '1';
}

function comoDataIsoOuNula(valor) {
  if (valor === null || valor === undefined || valor === '' || valor === '0001-01-01 00:00:00') return null;
  const stringData = String(valor).trim().replace(' ', 'T');
  const data = new Date(stringData);
  return Number.isNaN(data.getTime()) ? null : data.toISOString();
}

function comoNumero(valor) {
  return valor === null || valor === undefined || valor === '' ? 0 : Number(valor);
}

const GUID_VAZIO = '00000000-0000-0000-0000-000000000000';
function comoGuid(valor) {
  return valor || GUID_VAZIO;
}

function normalizarMesaViewModel(linha) {
  return {
    id: linha.Id,
    notes: linha.Notes || null,
    createdAt: comoDataIsoOuNula(linha.CreatedAt) || new Date(0).toISOString(),
    updatedAt: comoDataIsoOuNula(linha.UpdatedAt) || new Date(0).toISOString(),
    updatedBy: linha.UpdatedBy || GUID_VAZIO,
    isDeleted: comoBooleano(linha.IsDeleted),
    code: linha.Code || '',
    place: linha.PlaceDesignation || '',
    priceTypeEnum: 0,
    designation: linha.Designation || '',
    status: Number.isFinite(Number(linha.Status)) ? Number(linha.Status) : 0,
    opennedAt: comoDataIsoOuNula(linha.OpennedAt),
    placeId: linha.PlaceId || GUID_VAZIO
  };
}

function normalizarArtigoViewModel(linha) {
  return {
    id: linha.Id,
    code: String(linha.Code ?? ''),
    isComposed: comoBooleano(linha.IsComposed),
    isSdrPackaging: comoBooleano(linha.IsSdrPackaging),
    family: linha.FamilyDesignation || '',
    familyId: comoGuid(linha.FamilyId),
    vatRateId: comoGuid(linha.VatOnTableId),
    vatExemptionReasonId: linha.VatExemptionReasonId || null,
    subfamily: linha.SubfamilyDesignation || '',
    subfamilyId: comoGuid(linha.SubfamilyId),
    designation: linha.Designation || '',
    typeId: comoGuid(linha.TypeId),
    type: linha.TypeDesignation || '',
    classId: comoGuid(linha.ClassId),
    measurementUnitId: comoGuid(linha.MeasurementUnitId),
    sizeUnitId: comoGuid(linha.SizeUnitId),
    vatDirectSellingId: comoGuid(linha.VatDirectSellingId),
    buttonLabel: linha.Button_Label || null,
    defaultQuantity: comoNumero(linha.DefaultQuantity),
    minimumStock: comoNumero(linha.MinimumStock),
    price1: comoNumero(linha.Price1_Value),
    price2: comoNumero(linha.Price2_Value),
    price3: comoNumero(linha.Price3_Value),
    price4: comoNumero(linha.Price4_Value),
    price5: comoNumero(linha.Price5_Value),
    price1PromotionValue: comoNumero(linha.Price1_PromotionValue),
    price2PromotionValue: comoNumero(linha.Price2_PromotionValue),
    price3PromotionValue: comoNumero(linha.Price3_PromotionValue),
    price4PromotionValue: comoNumero(linha.Price4_PromotionValue),
    price5PromotionValue: comoNumero(linha.Price5_PromotionValue),
    price1UsePromotion: comoBooleano(linha.Price1_UsePromotion),
    price2UsePromotion: comoBooleano(linha.Price2_UsePromotion),
    price3UsePromotion: comoBooleano(linha.Price3_UsePromotion),
    price4UsePromotion: comoBooleano(linha.Price4_UsePromotion),
    price5UsePromotion: comoBooleano(linha.Price5_UsePromotion),
    vatDirectSelling: linha.VatDirectSelling || null,
    discount: comoNumero(linha.Discount),
    unit: linha.UnitAcronym || '',
    button: {
      label: linha.Button_Label || null,
      image: linha.Button_Image || null,
      imageExtension: linha.Button_ImageExtension || null
    },
    priceWithVat: comoBooleano(linha.PriceWithVat),
    classAcronym: linha.ClassAcronym || '',
    order: Number.isFinite(Number(linha.Order)) ? Number(linha.Order) : 0
  };
}

const CAMPOS_BOOLEANOS_GLOBAIS = new Set();

function coletarCamposBooleanos(schema, schemas, visitados = new Set()) {
  if (!schema || typeof schema !== 'object' || visitados.has(schema)) return;
  visitados.add(schema);

  if (schema.$ref) {
    const nome = schema.$ref.split('/').pop();
    coletarCamposBooleanos(schemas[nome], schemas, visitados);
    return;
  }
  if (Array.isArray(schema.allOf)) {
    schema.allOf.forEach(parte => coletarCamposBooleanos(parte, schemas, visitados));
  }
  if (schema.properties) {
    for (const [nomePropriedade, propriedade] of Object.entries(schema.properties)) {
      if (propriedade && propriedade.type === 'boolean') {
        CAMPOS_BOOLEANOS_GLOBAIS.add(nomePropriedade.toLowerCase());
      }
    }
  }
}

function normalizarParaContrato(objeto) {
  if (typeof objeto === 'boolean') return objeto;
  if (!objeto) return objeto;
  if (Array.isArray(objeto)) return objeto.map(normalizarParaContrato);
  if (typeof objeto !== 'object') return objeto;

  const novoObjeto = {};
  const camposBooleanos = [
    'passwordreset', 'isdeleted', 'islicensed', 'stocksmodule',
    'agtfemodule', 'hasexpired', 'isvalid', 'isdefault', 'iscomposed',
    'uniquearticles', 'issdrpackaging', 'favorit', 'favorite', 'useweighingbalance',
    'pvpvariable', 'pricewithvat', 'required', 'isactive', 'isclosed'
  ];

  for (const chave in objeto) {
    let valor = objeto[chave];
    const chaveMinuscula = chave.toLowerCase();

    if (camposBooleanos.includes(chaveMinuscula) || CAMPOS_BOOLEANOS_GLOBAIS.has(chaveMinuscula)) {
      valor = valor === 1 || valor === true || valor === '1';
    }

    if (chaveMinuscula.endsWith('date') || chaveMinuscula.endsWith('at') || chaveMinuscula === 'dateofcontract' || chaveMinuscula === 'birthdate' || chaveMinuscula === 'startdate' || chaveMinuscula === 'enddate') {
      if (valor === null || valor === undefined || valor === '' || valor === 'null' || valor === 'NULL' || valor === '0001-01-01 00:00:00') {
        valor = null;
      } else {
        try {
          let stringData = String(valor).trim();
          if (!stringData.includes('T')) stringData = stringData.replace(' ', 'T');
          const data = new Date(stringData);
          valor = !isNaN(data.getTime()) ? data.toISOString() : null;
        } catch (err) {
          valor = null;
        }
      }
    }

    const chaveCamel = chave.charAt(0).toLowerCase() + chave.slice(1);
    novoObjeto[chaveCamel] = (typeof valor === 'object' && valor !== null) ? normalizarParaContrato(valor) : valor;
  }
  return novoObjeto;
}

function normalizarSubfamiliaArtigo(linha) {
  return {
    ...normalizarParaContrato(linha),
    commissionGroup: null,
    discountGroup: null,
    vatOnTable: null,
    vatDirectSelling: null,
    button: {
      label: linha.Button_Label || null,
      image: linha.Button_Image || null,
      imageExtension: linha.Button_ImageExtension || null
    }
  };
}

function criarNormalizadores(db) {
  const SQL_ARTIGO_BASE = `
    SELECT a.*,
      sf.FamilyId AS FamilyId,
      sf.Designation AS SubfamilyDesignation,
      fam.Designation AS FamilyDesignation,
      t.Designation AS TypeDesignation,
      c.Acronym AS ClassAcronym,
      mu.Acronym AS UnitAcronym
    FROM "Articles" a
    LEFT JOIN "ArticleSubfamilies" sf ON sf.Id = a.SubfamilyId
    LEFT JOIN "ArticleFamilies" fam ON fam.Id = sf.FamilyId
    LEFT JOIN "ArticleTypes" t ON t.Id = a.TypeId
    LEFT JOIN "ArticleClasses" c ON c.Id = a.ClassId
    LEFT JOIN "MeasurementUnits" mu ON mu.Id = a.MeasurementUnitId
  `;

  async function buscarArtigoPorId(articleId) {
    if (!articleId) return null;
    const linha = await db.get(`${SQL_ARTIGO_BASE} WHERE a.Id = ?`, [articleId]);
    return linha ? normalizarArtigoViewModel(linha) : null;
  }

  async function buscarMesaPorId(tableId) {
    if (!tableId) return null;
    const linha = await db.get(`
      SELECT t.*, p.Designation AS PlaceDesignation
      FROM "Tables" t
      LEFT JOIN "Places" p ON p.Id = t.PlaceId
      WHERE t.Id = ?
    `, [tableId]);
    return linha ? normalizarMesaViewModel(linha) : null;
  }

  async function normalizarOrderDetailViewModel(linha) {
    return {
      id: linha.Id,
      designation: linha.Designation || '',
      article: await buscarArtigoPorId(linha.ArticleId),
      quantity: comoNumero(linha.Quantity),
      price: comoNumero(linha.Price),
      discount: comoNumero(linha.Discount),
      vat: comoNumero(linha.Vat),
      totalFinal: comoNumero(linha.TotalFinal)
    };
  }

  async function normalizarTicketViewModel(ticket) {
    const detalhes = await db.all(
      'SELECT * FROM "OrderDetails" WHERE TicketId = ? AND (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY "Order"',
      [ticket.Id]
    );
    return {
      id: ticket.Id,
      ticketId: Number.isFinite(Number(ticket.TicketId)) ? Number(ticket.TicketId) : 0,
      details: await Promise.all(detalhes.map(normalizarOrderDetailViewModel))
    };
  }

  async function normalizarOrderViewModel(order) {
    const tickets = await db.all(
      'SELECT * FROM "Tickets" WHERE OrderId = ? AND (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY TicketId',
      [order.Id]
    );
    return {
      id: order.Id,
      table: await buscarMesaPorId(order.TableId),
      tickets: await Promise.all(tickets.map(normalizarTicketViewModel))
    };
  }

  return { SQL_ARTIGO_BASE, normalizarOrderViewModel };
}

module.exports = {
  CAMPOS_BOOLEANOS_GLOBAIS, coletarCamposBooleanos, normalizarParaContrato,
  normalizarFamiliaArtigo, normalizarSubfamiliaArtigo, normalizarMesaViewModel,
  normalizarArtigoViewModel, comoBooleano, comoNumero, criarNormalizadores
};