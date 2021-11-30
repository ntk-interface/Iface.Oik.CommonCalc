// Расчет достоверности каналов измерения РЗА

// рекомендуемый период запуска 30сек, задержка 0
// параметр1 - список (n) ТИ каналов измерения одного и того-же значения (первым задается образцовый)
// параметр2 - список ТИ для результата "среднесуточная ошибка" для каждого канала ( n-1 )
// параметр3 - список ТИ для результата "изменение коэффициента передачи" для каждого канала ( n-1 )
const Debug = 1; // для ускорения проверки - работаем на каждом цикле и архив за последние 24 минуты

const input = InitTmAnalogGroupInput(0);
const outputK = InitTmAnalogGroupOutput(1);
const output3D = InitTmAnalogGroupOutput(2);

const inputAsu = input[0];
const inputRza = input.slice(1);
const inputRzaCount = inputRza.length;

let RETRO_STEP = 60 * 30;
const RETRO_COUNT = 50;
const MIN_VAL = 0.00001; // минимальное значение ТИ для анализа иначе деление на 0

// Определяем время запуска расчета первый раз
let CycleTime = new Date();
  // округляем до получаса
if (CycleTime.getMinutes() >= 30) 	CycleTime.setMinutes(30, 0, 0);
else     							CycleTime.setMinutes(0, 0, 0);
CycleTime = CycleTime + (31 * 60 * 1000); // ставим старт расчета на 1 или 31 минуту часа

function DoWork()
{
  const date = new Date();
  
  // если время не пришло то выходим
  if( !Debug )
	  if (date < CycleTime) return;
  
  // округляем до получаса
  if( !Debug )
  {
	if (date.getMinutes() >= 30) 	date.setMinutes(30, 0, 0);
	else 						    date.setMinutes(0, 0, 0);
  }
  else{
	date.setSeconds( 0, 0);
	RETRO_STEP = 60; // архив для отладки 1 мин
  }
  
  CycleTime = date + (31 * 60 * 1000) // ставим старт расчета на 1 или 31 минуту часа

  // получаем нужное время в секундах
  const retroEndTime = Math.floor(date.getTime() / 1000) - date.getTimezoneOffset() * 60;
  const retroStartTime = retroEndTime - RETRO_STEP * (RETRO_COUNT - 1);
  
  // чтение ретроспективы АСУ ТП
  LogDebug("Чтение архива АСУ");
  ClearErrorFlag();
  const retroAsu = GetTmAnalogRetro(inputAsu, retroStartTime, retroEndTime, RETRO_STEP);
  for (let t = 0; t < RETRO_COUNT; t++) if (Math.abs(retroAsu[t]) < MIN_VAL) retroAsu[t] = MIN_VAL;
  let ErrorAsu = 0;
  if( IsErrorFlagRaised() ) ErrorAsu = 1;
  
  let retroRza = [];
  for (let k = 0; k < inputRzaCount; k++)
  {
	// чтение ретроспективы РЗА
	ClearErrorFlag();
	LogDebug("Чтение архива РЗА");
	retroRza = GetTmAnalogRetro(inputRza[k], retroStartTime, retroEndTime, RETRO_STEP);
	for (let t = 0; t < RETRO_COUNT; t++) if (Math.abs(retroRza[t]) < MIN_VAL) retroRza[t] = MIN_VAL;
	let kRzaAsu = [];
	
	if( IsErrorFlagRaised() ||  (ErrorAsu == 1) ) // что-то с данными плохо
	{
		SetTmAnalog( output3D[k], 0 );
		SetTmAnalog( outputK[k], 0 );
		ClearTmAnalogFlags( inputRza[k],TmFlagAbnormal);
		LogDebug("Ошибка чтения архива РЗА - пропускаем этот канал");
		continue;
	}
	
	// среднее полтора часа назад
	kRzaAsu[0] = 0;
	
	for (let t = 0; t < 48; t++) 
	{
	kRzaAsu[0] = kRzaAsu[0] + retroRza[t]/retroAsu[t];
	//LogDebug("Расчет K("+t+"):"+ kRzaAsu[0].toFixed(5) +": "+retroRza[t].toFixed(5)+"--"+retroAsu[t].toFixed(5));
	}
	kRzaAsu[0] = kRzaAsu[0]/48;

	  // среднее час назад
	kRzaAsu[1] = 0;
	for (let t = 1; t < 49; t++) kRzaAsu[1] = kRzaAsu[1] + retroRza[t]/retroAsu[t];
	kRzaAsu[1] = kRzaAsu[1]/48;

	  // среднее полчаса назад
	kRzaAsu[2] = 0;
	for (let t = 2; t < 50; t++) kRzaAsu[2] = kRzaAsu[2] + retroRza[t]/retroAsu[t];
	kRzaAsu[2] = kRzaAsu[2]/48;
	
	// Вычисление текущей среднесуточной относительной ошибки
	const RzaErrK = Math.abs( kRzaAsu[2]-1 );
	LogDebug("вывод K: "+ RzaErrK.toFixed(3)+" ("+kRzaAsu[0].toFixed(3)+"; "+ kRzaAsu[1].toFixed(3)+"; "+kRzaAsu[2].toFixed(3)+")");
	SetTmAnalog( outputK[k], RzaErrK );
	
	// Вычисление нестационарной ошибки измерения
	let Trza = [];
	let Drza = [];
	for (let i = 0; i < 3; i++)
	{
		Trza[i] = retroRza[47+i]/kRzaAsu[i];
		Drza[i] = Math.abs( (Trza[i] - retroAsu[47+i])/Trza[i] );
	}
	const RzaErrD = Drza[0]+Drza[1]+Drza[2];
	LogDebug("вывод D: "+RzaErrD.toFixed(3)+" ("+Drza[0].toFixed(3)+"; "+Drza[1].toFixed(3)+"; "+Drza[2].toFixed(3)+")");
	SetTmAnalog( output3D[k], RzaErrD );
	
	// проверка границ допустимых отклонений
	if( (RzaErrK >= 0.05) || (RzaErrD >= 0.09) )
	{
		RaiseTmAnalogFlags( inputRza[k],TmFlagAbnormal);
	}
	else
	{
		ClearTmAnalogFlags( inputRza[k],TmFlagAbnormal);
	}
  }
}