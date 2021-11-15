const tsOn = InitTmStatusGroupOutput(0);

const tsOff = InitTmStatusGroupOutput(1);

function DoWork()
{
  for (let ts of tsOn)
  {
    SetTmStatus(ts, 1);
  }
  for (let ts of tsOff)
  {
    SetTmStatus(ts, 0);
  }
}