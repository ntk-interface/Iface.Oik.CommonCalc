const input = InitTmAnalogGroupOutput(0);
const output = InitTmAnalogGroupOutput(1);

const inputAsu = input[0];
const inputRza = input.slice(1);
const inputRzaCount = inputRza.length;

const RETRO_STEP = 60 * 30;
const RETRO_COUNT = 50;

function DoWork()
{
  const date = new Date();
  
  // округляем до получаса
  if (date.getMinutes() >= 30)
  {
    date.setMinutes(30, 0, 0);
  }
  else {
    date.setMinutes(0, 0, 0);
  }

  // получаем нужное время в секундах
  const retroEndTime = Math.floor(date.getTime() / 1000) - date.getTimezoneOffset() * 60;
  const retroStartTime = retroEndTime - RETRO_STEP * (RETRO_COUNT - 1);
  
  // ретроспектива АСУ ТП
  const retroAsu = GetTmAnalogRetro(inputAsu, retroStartTime, retroEndTime, RETRO_STEP);
  
  // ретроспективы РЗА
  const retroRza = [];
  for (let k = 0; k < inputRzaCount; k++)
  {
    retroRza.push(GetTmAnalogRetro(inputRza[k], retroStartTime, retroEndTime, RETRO_STEP));
  }
  
  for (let t = 0; t < RETRO_COUNT; t++)
  {
    // retroAsu[t]
    for (let k = 0; k < inputRzaCount; k++)
    {
      // retroRza[k][t]
    }
  }
  
  // заносим результаты вычислений
  SetTmAnalog(output[0], 0.05);
  SetTmAnalog(output[1], 0.09);
}