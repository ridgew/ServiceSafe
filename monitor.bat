rem sc delete "ServiceSafe"
sc create "ServiceSafe" binpath= "%CD%\ServiceSafe.exe" type= own start= auto DisplayName= "ServiceSafe"

sc description "ServiceSafe" "服务状态轮询，如发现被停止则自动重新启动。"
rem ServiceSafe  CLRSvrHost 5000
pause