namespace APPCORE;

public abstract class QueryClass : TransactionalClass
{
	public abstract string GetQuery();
	public abstract List<T> Get<T>();
	public T? Find<T>() 
	{
        var result =  Get<T>();
       return result.Count > 0  ? result[0] : default;       
    }
}
