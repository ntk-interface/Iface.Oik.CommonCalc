const input = GetGroupArray(0);
const inputIsAllowed = input[0];

const output = GetGroupArray(1);
const outputYear = output[0];
const outputMonth = output[1];
const outputDay = output[2];
const outputDayOfWeek = output[3];
const outputHour = output[4];
const outputMinute  = output[5];
const outputSecond = output[6];

function DoWork()
{
  // если ТС разрешения дорасчета снят, ничего не делаем
  if (IsTmStatusOff(inputIsAllowed))
  {
    return;
  }
  
  // определяем все составляющие даты и времени
  const dateTime  = new Date();
  const dayOfWeek = dateTime.getDay();
  const day       = dateTime.getDate();
  const month     = dateTime.getMonth() + 1;
  const year      = dateTime.getFullYear();
  const hour      = dateTime.getHours();
  const minute    = dateTime.getMinutes();
  const second    = dateTime.getSeconds();

  // записываем значения в измерения с адресами 24:1:1..7
  SetTmAnalog(outputYear, year);
  SetTmAnalog(outputMonth, month);
  SetTmAnalog(outputDay, day);
  SetTmAnalog(outputDayOfWeek, dayOfWeek);
  SetTmAnalog(outputHour, hour);
  SetTmAnalog(outputMinute, minute);
  SetTmAnalog(outputSecond, second);
}
