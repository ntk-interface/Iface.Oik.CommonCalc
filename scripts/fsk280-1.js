const intervalToKeep = 60 * 30 * 1000;
const pointsToKeep   = Math.ceil(intervalToKeep / GetPeriod());

const inputI  = InitTmAnalogGroupInput(0);
const inputP  = InitTmAnalogGroupInput(1);
const inputKF = InitTmAnalogGroupInput(2);

const inputINi  = InitTmAnalogGroupInput(3);
const inputKTTi = InitTmAnalogGroupInput(4);
const inputKNAi = InitTmAnalogGroupInput(5);
const inputIPi  = InitTmAnalogGroupInput(6);

const output = InitTmAnalogGroupOutput(9);

if (inputI.length !== inputP.length ||
    inputI.length !== inputKF.length ||
    inputI.length !== inputINi.length ||
    inputI.length !== inputKTTi.length ||
    inputI.length !== inputKNAi.length ||
    inputI.length !== inputIPi.length)
{
  ThrowException("Не совпадают размеры групп входных параметров");
}
const inputLength = inputI.length;

// подготавливаем массивы данных
const Ii  = [];
const Pi  = [];
const KFi = [];
const errors = [];
for (let i = 0; i < inputLength; i++)
{
  Ii.push([]);
  Pi.push([]);
  KFi.push([]);
}

function DoWork()
{
  // заполняем текущими значениями
  for (let i = 0; i < inputLength; i++)
  {
    Ii[i].push(GetTmAnalog(inputI[i]));
    Pi[i].push(GetTmAnalog(inputP[i]));
    KFi[i].push(GetTmAnalog(inputKF[i]));
    errors.push(IsErrorFlagRaised());
  }
  if (Ii[0].length < pointsToKeep) // если точек недостаточно для расчета, продолжаем копить
  {
    LogDebug('Продолжаю накалпивать значения, пропуск расчета...');
    return;
  }

  // заполняем справочные данные
  const INi  = [];
  const KTTi = [];
  const KNAi = [];
  const IPi  = [];
  for (let i = 0; i < inputLength; i++)
  {
    INi[i]  = GetTmAnalog(inputINi[i]);
    KTTi[i] = GetTmAnalog(inputKTTi[i]);
    KNAi[i] = GetTmAnalog(inputKNAi[i]);
    IPi[i]  = GetTmAnalog(inputIPi[i]);
  }

  // определяем средние и первую часть расчета
  const SIi  = [];
  const SPi  = [];
  const SKFi = [];
  let SP     = 0;
  let NFP    = 0;
  for (let i = 0; i < inputLength; i++)
  {
    // удаляем устаревшие значения
    Ii[i].shift();
    Pi[i].shift();
    KFi[i].shift();
    errors.shift();

    SIi[i]  = getArrayAverage(Ii[i]);
    SPi[i]  = getArrayAverage(Pi[i]);
    SKFi[i] = getArrayAverage(KFi[i]);

    SP += Math.abs(SPi[i]);
    NFP += SPi[i];
  }
  
  // если найдено хоть одно недостоверное значение, выставляем флаг недостовености и выходим
  if (errors.some(e => e === true))
  {
    LogDebug('Найдено недостоверное значение, невозможно выполнить расчет');
    RaiseTmAnalogFlags(output[0], TmFlagUnreliable);
    return;
  }

  // дальнейший расчет
  let PG2 = 0;
  for (let i = 0; i < inputLength; i++)
  {
    const DP  = Math.abs(SPi[i]) / SP;
    const D   = SIi[i] / INi[i];
    const PL  = 0.5;
    const KNU = KNAi[i] === 0.5 ? 20 : 10;

    let KTA = 0;
    let KTU = 0;
    if (KTTi[i] === 0.5)
    {
      if (D >= 1)
      {
        KTA = 0.5;
        KTU = 30;
      }
      else if (D < 0.2)
      {
        KTA = 1.5;
        KTU = 90;
      }
      else
      {
        KTA = 0.75;
        KTU = 45;
      }
    }
    else
    {
      if (D >= 1)
      {
        KTA = 0.2;
        KTU = 10;
      }
      else if (D < 0.2)
      {
        KTA = 0.75;
        KTU = 30;
      }
      else
      {
        KTA = 0.35;
        KTU = 15;
      }
    }
    const PIP = IPi[i];
    const UP  = 0.029 * sqrt(pow2(KNU) + pow2(KTU)) * sqrt(1 - pow2(SKFi[i])) / SKFi[i];
    const PIK = 1.1 * sqrt(pow2(KTA) + pow2(KNAi[i]) + pow2(PL) + pow2(UP) + pow2(PIP));

    PG2 += (pow2(DP) * pow2(PIK));
  }
  const PG  = sqrt(PG2);
  const NDP = PG * SP / 100;

  // занесение результата
  SetTmAnalog(output[0], NFP / NDP);
}

function sqrt(num)
{
  return Math.sqrt(num);
}

function pow2(num)
{
  return Math.pow(num, 2);
}

function getArrayAverage(arr)
{
  const sum = arr.reduce((a, b) => a + b, 0);
  return sum / arr.length;
}