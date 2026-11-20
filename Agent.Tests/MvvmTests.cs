using System;
using System.Collections.Generic;
using NUnit.Framework;
using Agent.Mvvm;

namespace Agent.Tests
{
    [TestFixture]
    public class RelayCommandTests
    {
        [Test]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null));
        }

        [Test]
        public void Execute_InvokesDelegate()
        {
            object received = null;
            var cmd = new RelayCommand(p => received = p);

            cmd.Execute("payload");

            Assert.That(received, Is.EqualTo("payload"));
        }

        [Test]
        public void CanExecute_WithoutPredicate_ReturnsTrue()
        {
            var cmd = new RelayCommand(_ => { });
            Assert.That(cmd.CanExecute(null), Is.True);
        }

        [Test]
        public void CanExecute_WithPredicate_ReturnsPredicate()
        {
            var cmd = new RelayCommand(_ => { }, p => (int)p > 0);
            Assert.That(cmd.CanExecute(1), Is.True);
            Assert.That(cmd.CanExecute(0), Is.False);
        }

        [Test]
        public void RaiseCanExecuteChanged_RaisesEvent()
        {
            var cmd = new RelayCommand(_ => { });
            var raised = 0;
            cmd.CanExecuteChanged += (s, e) => raised++;

            cmd.RaiseCanExecuteChanged();

            Assert.That(raised, Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class BaseEntityTests
    {
        private sealed class Entity : BaseEntity
        {
            private string _value;

            public string Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }
        }

        [Test]
        public void IsPropertyChangedNull_ReflectsSubscribers()
        {
            var entity = new Entity();
            Assert.That(entity.IsPropertyChangedNull, Is.True);

            entity.PropertyChanged += (s, e) => { };
            Assert.That(entity.IsPropertyChangedNull, Is.False);
        }

        [Test]
        public void OnPropertyChanged_RaisesEvent()
        {
            var entity = new Entity();
            string prop = null;
            entity.PropertyChanged += (s, e) => prop = e.PropertyName;

            entity.OnPropertyChanged("Name");

            Assert.That(prop, Is.EqualTo("Name"));
        }

        [Test]
        public void SetProperty_Changed_RaisesAndReturnsTrue()
        {
            var entity = new Entity();
            string prop = null;
            entity.PropertyChanged += (s, e) => prop = e.PropertyName;

            entity.Value = "a";

            Assert.That(prop, Is.EqualTo("Value"));
            Assert.That(entity.Value, Is.EqualTo("a"));
        }

        [Test]
        public void SetProperty_Unchanged_ReturnsFalseAndDoesNotRaise()
        {
            var entity = new Entity { Value = "a" };
            var raised = 0;
            entity.PropertyChanged += (s, e) => raised++;

            entity.Value = "a";

            Assert.That(raised, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class BaseModelTests
    {
        private sealed class Model : BaseModel
        {
            private string _value;

            public string Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }

            public int CallCount { get; private set; }

            public void Ping() => CallCount++;

            public void Add(int a, int b) => CallCount = a + b;
        }

        [Test]
        public void ShowDialogMessage_RaisesEvent()
        {
            var model = new Model();
            string received = null;
            model.ShowDialog += (s, m) => received = m;

            model.ShowDialogMessage("hello");

            Assert.That(received, Is.EqualTo("hello"));
        }

        [Test]
        public void NotifyMessage_RaisesEvent()
        {
            var model = new Model();
            Notify received = null;
            model.Notify += (s, n) => received = n;

            model.NotifyMessage(new Notify { Message = "m", NotifyLevel = NotifyLevel.Warning });

            Assert.That(received.Message, Is.EqualTo("m"));
            Assert.That(received.NotifyLevel, Is.EqualTo(NotifyLevel.Warning));
        }

        [Test]
        public void LifecycleMethods_AreNoOp()
        {
            var model = new Model();
            Assert.DoesNotThrow(() =>
            {
                model.Run();
                model.AfterLoaded();
                model.Loaded();
                model.Closing();
            });
        }

        [Test]
        public void SendCommand_ByName_InvokesParameterlessMethod()
        {
            var model = new Model();
            model.SendCommand("Ping");
            Assert.That(model.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void SendCommand_WithArguments_InvokesMethod()
        {
            var model = new Model();
            model.SendCommand(new List<object> { "Add", 2, 3 });
            Assert.That(model.CallCount, Is.EqualTo(5));
        }

        [Test]
        public void SetProperty_ForwardsPropertyChangedToViewModel()
        {
            var model = new Model();
            var vm = new TestViewModel();
            string forwarded = null;
            vm.PropertyChanged += (s, e) => forwarded = e.PropertyName;

            model.ViewModel = vm;
            model.Value = "x";

            Assert.That(forwarded, Is.EqualTo("Value"));
        }
    }

    [TestFixture]
    public class BaseViewModelTests
    {
        private sealed class ViewModel : BaseViewModel
        {
            public bool Initialized { get; private set; }

            public override void InitializeViewModel() => Initialized = true;
        }

        private sealed class Model : BaseModel
        {
            public int CallCount { get; private set; }

            public void Ping() => CallCount++;
        }

        [Test]
        public void SetModel_WiresModelViewAndViewModel()
        {
            var model = new Model();
            var vm = new ViewModel();
            var view = new FakeView();

            vm.SetModel(model, view);

            Assert.That(vm.Model, Is.SameAs(model));
            Assert.That(vm.View, Is.SameAs(view));
            Assert.That(model.ViewModel, Is.SameAs(vm));
            Assert.That(vm.Initialized, Is.True);
        }

        [Test]
        public void Cmd_ForwardsToModel()
        {
            var model = new Model();
            var vm = new ViewModel();
            vm.SetModel(model, new FakeView());

            vm.Cmd.Execute("Ping");

            Assert.That(model.CallCount, Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class MvvmEnumsTests
    {
        [Test]
        public void NotifyLevel_Values()
        {
            Assert.That((int)NotifyLevel.Trace, Is.EqualTo(0));
            Assert.That((int)NotifyLevel.Info, Is.EqualTo(1));
            Assert.That((int)NotifyLevel.Success, Is.EqualTo(2));
            Assert.That((int)NotifyLevel.Warning, Is.EqualTo(3));
            Assert.That((int)NotifyLevel.Error, Is.EqualTo(4));
        }

        [Test]
        public void ViewMode_Values()
        {
            Assert.That((int)ViewMode.Show, Is.EqualTo(0));
            Assert.That((int)ViewMode.ShowDialog, Is.EqualTo(1));
        }

        [Test]
        public void Notify_Defaults()
        {
            var notify = new Notify();
            Assert.That(notify.Message, Is.Null);
            Assert.That(notify.NotifyLevel, Is.EqualTo(NotifyLevel.Trace));
        }
    }

    internal sealed class TestViewModel : BaseViewModel
    {
    }

    internal sealed class FakeView : IView
    {
        public bool? ShowDialog() => true;

        public void Show() { }

        public void NotifyMessage(Notify notify) { }

        public bool? ShowDialogMessage(string message) => true;
    }
}
