rem sc delete "ServiceSafe"
sc create "ServiceSafe" binpath= "%CD%\ServiceSafe.exe" type= own start= auto DisplayName= "ServiceSafe"

sc description "ServiceSafe" "����״̬��ѯ���緢�ֱ�ֹͣ���Զ�����������"
rem ServiceSafe  CLRSvrHost 5000
pause