namespace APPCORE;

public abstract class QueryClass : TransactionalClass
{
	public abstract string GetQuery();
	public abstract List<T> Get<T>();
}
