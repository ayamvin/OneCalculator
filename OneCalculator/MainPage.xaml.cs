namespace OneCalculator
{
    public partial class MainPage : ContentPage
    {
        private bool _isResultExpanded = false;
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
        
    }

}
