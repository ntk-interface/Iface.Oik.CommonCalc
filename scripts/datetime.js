const output = InitTmAnalogGroupOutput(0);

function DoWork()
{
  // определяем текущие дату и время
  const dateTime  = new Date();

  // записываем значения в измерения
  SetTmAnalog(output[0], dateTime.getFullYear());
  SetTmAnalog(output[1], dateTime.getMonth() + 1);
  SetTmAnalog(output[2], dateTime.getDate());
  SetTmAnalog(output[3], dateTime.getHours());
  SetTmAnalog(output[4], dateTime.getMinutes());
  SetTmAnalog(output[5], dateTime.getSeconds());
}
