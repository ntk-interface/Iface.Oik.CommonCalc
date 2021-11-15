const input = InitTmStatusGroupInput(0);
const inputIsAllowed = input[0];

const output = InitTmAnalogGroupOutput(1);
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
  
  // определяем текущие дату и время
  const dateTime  = new Date();

  // записываем значения в измерения
  SetTmAnalog(outputYear, dateTime.getFullYear());
  SetTmAnalog(outputMonth, dateTime.getMonth() + 1);
  SetTmAnalog(outputDay, dateTime.getDate());
  SetTmAnalog(outputDayOfWeek, dateTime.getDay());
  SetTmAnalog(outputHour, dateTime.getHours());
  SetTmAnalog(outputMinute, dateTime.getMinutes());
  SetTmAnalog(outputSecond, dateTime.getSeconds());
}
